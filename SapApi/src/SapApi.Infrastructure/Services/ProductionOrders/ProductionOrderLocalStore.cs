using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services.ProductionOrders;

public record ProductionOrderSyncResult(
    string CompanyDb,
    int UpsertedCount,
    int PageCount,
    DateTime SyncedAtUtc,
    string Message,
    string Mode = "full",
    int AddedCount = 0,
    int UpdatedCount = 0,
    int? AbsoluteEntry = null,
    /// <summary>True when the batch stopped early and the caller should sync again to continue.</summary>
    bool HasMore = false,
    /// <summary>Highest AbsoluteEntry processed — pass back as afterAbsoluteEntry to resume.</summary>
    int? LastAbsoluteEntry = null,
    string Status = ProductionOrderSyncState.StatusIdle,
    string? HangfireJobId = null,
    DateTime? StartedAtUtc = null);

/// <summary>
/// Read model for SAP production orders. Every portal read of production order data comes from
/// here; the only SAP traffic in this class is the sync itself.
/// </summary>
public class ProductionOrderLocalStore(
    AppDbContext db,
    IHttpRequestHandler requestHandler,
    ICurrentCompanyDbAccessor companyDbAccessor,
    SapMasterDataService masterDataService,
    IHttpContextAccessor httpContextAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    /// <summary>
    /// Each production order needs its own SAP detail call, so an unbounded sync runs for minutes
    /// and is killed by the reverse proxy. Work is capped per request and the caller resumes with
    /// <c>afterAbsoluteEntry</c>.
    /// </summary>
    private const int MaxRecordsPerBatch = 400;

    private static readonly TimeSpan BatchTimeBudget = TimeSpan.FromSeconds(25);

    /// <summary>Header page size for the AbsoluteEntry scan (keys only, so a wide page is cheap).</summary>
    private const int HeaderPageSize = 1000;

    /// <summary>
    /// Statuses whose orders can still change in SAP. Refreshing only these keeps the pickers
    /// accurate without re-reading closed history on every run.
    /// </summary>
    private static readonly string[] OpenStatuses =
    [
        Constants.SapProductionOrderStatus.Planned,
        Constants.SapProductionOrderStatus.Released,
    ];

    public async Task<PaginationResponse<List<SapProductionOrdersResponse>>> ListFromDbAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = db.ProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb);

        if (request.Sorts.Count == 0)
            query = query.OrderByDescending(x => x.AbsoluteEntry);

        var listRequest = new PaginationRequest
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Sorts = MapSortFields(request.Sorts),
            Filters = MapFilterFields(request.Filters),
        };

        var (items, totalCount) = await query.ToPaginatedListAsync(
            listRequest,
            cancellationToken,
            ProductionOrderMapper.ListOrFieldAliases);

        var data = items.Select(e => ProductionOrderMapper.ToSapResponse(e, includeLines: false)).ToList();
        return PaginationResponseFactory.Create(request, data, totalCount);
    }

    public async Task<SapProductionOrdersResponse?> GetFromDbAsync(
        int absoluteEntry,
        bool includeLines,
        CancellationToken cancellationToken = default)
    {
        var query = db.ProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == absoluteEntry);

        if (includeLines)
            query = query.Include(x => x.Lines);

        var entity = await query.FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ProductionOrderMapper.ToSapResponse(entity, includeLines);
    }

    public async Task<List<SapProductionOrderLines>> GetLinesFromDbAsync(
        int absoluteEntry,
        CancellationToken cancellationToken = default)
    {
        var order = await db.ProductionOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == absoluteEntry, cancellationToken);

        return order is null ? [] : ProductionOrderMapper.ToSapLines(order.AbsoluteEntry, order.Lines);
    }

    /// <summary>
    /// The UI sends the column keys it displays; the mirror columns are named after the entity.
    /// Only aliases that are not already an entity property are translated.
    /// </summary>
    private static readonly Dictionary<string, string> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ProductionOrderStatus"] = "Status",
        ["ItemNumber"] = "ItemNo",
        ["DocNum"] = "DocumentNumber",
        ["DocEntry"] = "AbsoluteEntry",
        ["CardCode"] = "CustomerCode",
        ["CardName"] = "CustomerName",
        ["U_PrjName"] = "ProjectName",
        ["U_DwgNo"] = "DrawingNo",
        ["U_ProdType"] = "ProductionCategory",
    };

    private static List<FilterModel> MapFilterFields(IEnumerable<FilterModel> filters) =>
        filters
            .Select(f => ColumnAliases.TryGetValue(f.Field, out var mapped)
                ? new FilterModel { Field = mapped, Operator = f.Operator, Value = f.Value }
                : f)
            .ToList();

    private static List<SortModel> MapSortFields(IEnumerable<SortModel> sorts) =>
        sorts
            .Select(s => ColumnAliases.TryGetValue(s.Field, out var mapped)
                ? new SortModel { Field = mapped, Direction = s.Direction }
                : s)
            .ToList();

    public async Task UpsertFromSapAsync(
        SapProductionOrdersResponse sap,
        ResolvedMasterNames? names = null,
        CancellationToken cancellationToken = default)
    {
        if (sap.AbsoluteEntry is null or <= 0)
            return;

        var now = DateTime.UtcNow;
        // Ignore soft-delete filter: row sync must revive an existing AbsoluteEntry and replace lines.
        // DbContext is globally NoTracking — track this row so header fields persist.
        var entity = await db.ProductionOrders
            .IgnoreQueryFilters()
            .AsTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == sap.AbsoluteEntry.Value,
                cancellationToken);

        var isNew = entity is null;
        if (entity is null)
        {
            entity = new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = sap.AbsoluteEntry.Value,
                CreatedOn = now,
            };
            db.ProductionOrders.Add(entity);
        }

        ProductionOrderMapper.ApplyHeader(entity, sap, now);
        ApplyResolvedNames(entity, names);

        if (isNew)
        {
            await db.SaveChangesAsync(cancellationToken);
            var lines = ProductionOrderMapper.MapLines(entity.Id, sap.ProductionOrderLines);
            entity.Lines = lines;
            db.ProductionOrderLines.AddRange(lines);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await ReplaceLinesFromSapAsync(entity, sap, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyResolvedNames(ProductionOrder entity, ResolvedMasterNames? names)
    {
        if (names is null)
            return;

        if (!string.IsNullOrWhiteSpace(entity.CustomerCode)
            && names.BusinessPartners.TryGetValue(entity.CustomerCode, out var cardName)
            && !string.IsNullOrWhiteSpace(cardName))
        {
            entity.CustomerName = cardName;
        }

        // U_PrjName wins when SAP has one; otherwise fall back to the project master name.
        if (string.IsNullOrWhiteSpace(entity.ProjectName)
            && !string.IsNullOrWhiteSpace(entity.Project)
            && names.Projects.TryGetValue(entity.Project, out var projectName)
            && !string.IsNullOrWhiteSpace(projectName))
        {
            entity.ProjectName = projectName;
        }
    }

    /// <summary>
    /// SAP sync replaces the full local snapshot. Hard-delete existing lines so soft-delete and the
    /// partial unique index cannot block a re-sync that reuses the same LineNumber values.
    /// </summary>
    private async Task ReplaceLinesFromSapAsync(
        ProductionOrder entity,
        SapProductionOrdersResponse sap,
        CancellationToken cancellationToken)
    {
        var productionOrderId = entity.Id;

        // ExecuteDeleteAsync bypasses the change tracker — detach stale children so SaveChanges
        // cannot resurrect rows that were hard-deleted from Postgres.
        foreach (var line in entity.Lines.ToList())
            db.Entry(line).State = EntityState.Detached;
        entity.Lines.Clear();

        if (db.Database.IsRelational())
        {
            await db.ProductionOrderLines
                .IgnoreQueryFilters()
                .Where(x => x.ProductionOrderId == productionOrderId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            // InMemory provider does not support ExecuteDeleteAsync; a remove-replace is enough for tests.
            var existing = await db.ProductionOrderLines
                .IgnoreQueryFilters()
                .Where(x => x.ProductionOrderId == productionOrderId)
                .ToListAsync(cancellationToken);
            db.ProductionOrderLines.RemoveRange(existing);
        }

        var newLines = ProductionOrderMapper.MapLines(productionOrderId, sap.ProductionOrderLines);
        entity.Lines = newLines;
        db.ProductionOrderLines.AddRange(newLines);
    }

    /// <summary>Pulls one complete production order from SAP and upserts the local row.</summary>
    public async Task<ProductionOrderSyncResult> SyncOneFromSapAsync(
        int absoluteEntry,
        CancellationToken cancellationToken = default)
    {
        if (absoluteEntry <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteEntry));

        var stopwatch = Stopwatch.StartNew();
        var existed = await db.ProductionOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == absoluteEntry, cancellationToken);

        SapProductionOrdersResponse? detail;
        try
        {
            detail = await requestHandler.GetOrThrowAsync<SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(absoluteEntry.ToString()),
                cancellationToken);
        }
        catch (Exception ex)
        {
            await WriteAuditAsync("one", absoluteEntry, 0, 0, false, ex.Message, stopwatch, cancellationToken);
            throw;
        }

        if (detail?.AbsoluteEntry is null)
        {
            const string notFound = "Production order was not found in SAP.";
            await WriteAuditAsync("one", absoluteEntry, 0, 0, false, notFound, stopwatch, cancellationToken);
            throw new ApiErrorException(
                BaseErrorCodes.ValidationFailed,
                $"Production order {absoluteEntry} was not found in SAP.");
        }

        var names = await ResolveMasterNamesAsync([detail], cancellationToken);
        await UpsertFromSapAsync(detail, names, cancellationToken);

        var syncedAt = DateTime.UtcNow;
        var added = existed ? 0 : 1;
        var updated = existed ? 1 : 0;
        var message = existed
            ? $"Updated production order {detail.DocumentNumber ?? absoluteEntry} from SAP."
            : $"Added production order {detail.DocumentNumber ?? absoluteEntry} from SAP.";

        await WriteAuditAsync("one", absoluteEntry, added, updated, true, message, stopwatch, cancellationToken);

        Log.Information(
            "Production order row sync for {CompanyDb} AbsoluteEntry {AbsoluteEntry}: {Action} in {DurationMs}ms",
            CompanyDb,
            absoluteEntry,
            existed ? "updated" : "added",
            stopwatch.ElapsedMilliseconds);

        return new ProductionOrderSyncResult(
            CompanyDb,
            UpsertedCount: 1,
            PageCount: 1,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: "one",
            AddedCount: added,
            UpdatedCount: updated,
            AbsoluteEntry: absoluteEntry);
    }

    /// <summary>
    /// Incremental sync: only AbsoluteEntries greater than the highest already stored locally.
    /// When the local table is empty this imports every production order from SAP.
    /// </summary>
    public Task<ProductionOrderSyncResult> SyncNewFromSapAsync(
        int? afterAbsoluteEntry = null,
        CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: true, afterAbsoluteEntry, cancellationToken);

    /// <summary>Full re-sync of every production order from SAP into the local tables.</summary>
    public Task<ProductionOrderSyncResult> SyncAllFromSapAsync(
        int? afterAbsoluteEntry = null,
        CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: false, afterAbsoluteEntry, cancellationToken);

    /// <summary>
    /// Re-pulls locally open orders (Planned / Released) so status, quantities and released dates
    /// stay current. A "new only" pass cannot see these changes because SAP exposes no
    /// last-updated field on ProductionOrders. Resumable via <paramref name="afterAbsoluteEntry"/>.
    /// </summary>
    public async Task<ProductionOrderSyncResult> SyncOpenOrdersFromSapAsync(
        int? afterAbsoluteEntry = null,
        CancellationToken cancellationToken = default)
    {
        var syncedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var afterExclusive = afterAbsoluteEntry ?? 0;
        var lastAbsoluteEntry = afterExclusive;
        var updated = 0;
        var missing = 0;
        var hasMore = false;

        // Keys only, and capped — never materialise the whole open set in memory.
        var candidates = await db.ProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb
                        && x.AbsoluteEntry > afterExclusive
                        && x.Status != null
                        && OpenStatuses.Contains(x.Status))
            .OrderBy(x => x.AbsoluteEntry)
            .Select(x => x.AbsoluteEntry)
            .Take(MaxRecordsPerBatch + 1)
            .ToListAsync(cancellationToken);

        if (candidates.Count > MaxRecordsPerBatch)
        {
            hasMore = true;
            candidates.RemoveAt(candidates.Count - 1);
        }

        try
        {
            foreach (var absoluteEntry in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastAbsoluteEntry = absoluteEntry;

                var detail = await requestHandler.GetOrThrowAsync<SapProductionOrdersResponse>(
                    Constants.SapApiUrls.GetProductionOrders(absoluteEntry.ToString()),
                    cancellationToken);

                if (detail?.AbsoluteEntry is null)
                {
                    missing++;
                    continue;
                }

                var names = await ResolveMasterNamesAsync([detail], cancellationToken);
                await UpsertFromSapAsync(detail, names, cancellationToken);
                updated++;

                if (stopwatch.Elapsed >= BatchTimeBudget)
                {
                    hasMore = true;
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = $"Open-order refresh failed after {updated} record(s): {ex.Message}";
            await SaveSyncStateAsync(syncedAt, updated, failure, cancellationToken, lastAbsoluteEntry);
            await WriteAuditAsync("open", null, 0, updated, false, failure, stopwatch, cancellationToken);
            throw;
        }

        var message = hasMore
            ? $"Refreshed {updated} open production order(s) up to entry {lastAbsoluteEntry}. More remaining."
            : updated == 0
                ? "No open production orders needed refreshing."
                : $"Refreshed {updated} open production order(s)"
                  + (missing > 0 ? $" ({missing} no longer in SAP)." : ".");

        await SaveSyncStateAsync(syncedAt, updated, message, cancellationToken, lastAbsoluteEntry);
        await WriteAuditAsync("open", null, 0, updated, true, message, stopwatch, cancellationToken);

        return new ProductionOrderSyncResult(
            CompanyDb,
            UpsertedCount: updated,
            PageCount: 0,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: "open",
            UpdatedCount: updated,
            HasMore: hasMore,
            LastAbsoluteEntry: lastAbsoluteEntry);
    }

    /// <summary>
    /// Finds integer holes between consecutive local AbsoluteEntries and pulls those numbers from
    /// SAP when they exist. Resume with <paramref name="afterAbsoluteEntry"/> (exclusive).
    /// </summary>
    public async Task<ProductionOrderSyncResult> SyncMissingGapsFromSapAsync(
        int? afterAbsoluteEntry = null,
        CancellationToken cancellationToken = default)
    {
        var syncedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var added = 0;
        var skippedAbsent = 0;
        var hasMore = false;
        var afterExclusive = afterAbsoluteEntry ?? 0;
        var lastAbsoluteEntry = afterExclusive;

        var sorted = await db.ProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .Select(x => x.AbsoluteEntry)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        if (sorted.Count < 2)
        {
            var emptyMessage = sorted.Count == 0
                ? "No local production orders yet; skipping gap fill."
                : "Only one local production order; no sequence gaps to fill.";
            await SaveSyncStateAsync(syncedAt, 0, emptyMessage, cancellationToken, sorted.LastOrDefault());
            return new ProductionOrderSyncResult(
                CompanyDb,
                UpsertedCount: 0,
                PageCount: 0,
                SyncedAtUtc: syncedAt,
                Message: emptyMessage,
                Mode: "gaps",
                HasMore: false,
                LastAbsoluteEntry: sorted.LastOrDefault());
        }

        try
        {
            foreach (var candidate in EnumerateIntegerGaps(sorted, afterExclusive))
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastAbsoluteEntry = candidate;

                var detail = await TryGetProductionOrderDetailAsync(candidate, cancellationToken);
                if (detail?.AbsoluteEntry is null)
                {
                    skippedAbsent++;
                }
                else
                {
                    var names = await ResolveMasterNamesAsync([detail], cancellationToken);
                    await UpsertFromSapAsync(detail, names, cancellationToken);
                    added++;
                }

                if (added + skippedAbsent >= MaxRecordsPerBatch || stopwatch.Elapsed >= BatchTimeBudget)
                {
                    hasMore = EnumerateIntegerGaps(sorted, candidate).Any();
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure =
                $"Gap sync failed after {added} record(s) ({skippedAbsent} absent in SAP): {ex.Message}";
            await SaveSyncStateAsync(syncedAt, added, failure, cancellationToken, lastAbsoluteEntry);
            await WriteAuditAsync("gaps", null, added, 0, false, failure, stopwatch, cancellationToken);
            Log.Error(
                ex,
                "Production order gap sync failed for {CompanyDb} after added={Added}, skippedAbsent={Skipped}",
                CompanyDb,
                added,
                skippedAbsent);
            throw;
        }

        var message = hasMore
            ? $"Filled {added} gap entry(ies) ({skippedAbsent} absent in SAP) up to {lastAbsoluteEntry}. More gaps remaining."
            : added == 0 && skippedAbsent == 0
                ? "No missing entries in the local sequence."
                : $"Gap fill complete: {added} restored from SAP ({skippedAbsent} hole(s) had no SAP document).";

        await SaveSyncStateAsync(syncedAt, added, message, cancellationToken, lastAbsoluteEntry);
        await WriteAuditAsync("gaps", null, added, 0, true, message, stopwatch, cancellationToken);

        Log.Information(
            "Production order gap sync for {CompanyDb}: added={Added}, skippedAbsent={Skipped}, hasMore={HasMore}",
            CompanyDb,
            added,
            skippedAbsent,
            hasMore);

        return new ProductionOrderSyncResult(
            CompanyDb,
            UpsertedCount: added,
            PageCount: 0,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: "gaps",
            AddedCount: added,
            HasMore: hasMore,
            LastAbsoluteEntry: lastAbsoluteEntry);
    }

    /// <summary>
    /// Yields exclusive integer holes between consecutive sorted entries, greater than
    /// <paramref name="afterExclusive"/>.
    /// </summary>
    public static IEnumerable<int> EnumerateIntegerGaps(IReadOnlyList<int> sortedEntries, int afterExclusive)
    {
        for (var i = 0; i < sortedEntries.Count - 1; i++)
        {
            var from = sortedEntries[i];
            var to = sortedEntries[i + 1];
            if (to - from <= 1)
                continue;

            var start = Math.Max(from + 1, afterExclusive + 1);
            for (var missing = start; missing < to; missing++)
                yield return missing;
        }
    }

    private async Task<SapProductionOrdersResponse?> TryGetProductionOrderDetailAsync(
        int absoluteEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            return await requestHandler.GetOrThrowAsync<SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(absoluteEntry.ToString()),
                cancellationToken);
        }
        catch (ApiErrorException ex)
        {
            // Sequence holes are usually numbers that never existed as production orders in SAP.
            Log.Debug(
                ex,
                "No SAP production order for AbsoluteEntry {AbsoluteEntry} while filling local sequence gaps",
                absoluteEntry);
            return null;
        }
    }

    private async Task<ProductionOrderSyncResult> SyncFromSapInternalAsync(
        bool newOnly,
        int? afterAbsoluteEntry,
        CancellationToken cancellationToken)
    {
        var syncedAt = DateTime.UtcNow;
        var mode = newOnly ? "new" : "full";
        var added = 0;
        var updated = 0;
        var pages = 0;
        var hasMore = false;

        // Resume point: explicit cursor wins, otherwise incremental starts after the local max.
        var startEntry = afterAbsoluteEntry
            ?? (newOnly ? await GetMaxLocalAbsoluteEntryAsync(cancellationToken) : 0);
        var lastAbsoluteEntry = startEntry;

        var url = BuildSyncStartUrl(startEntry);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (!string.IsNullOrWhiteSpace(url))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages++;

                // GetOrThrowAsync: a swallowed failure here would end the loop early and be
                // reported to the user as a successful sync that imported nothing.
                var page = await requestHandler.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                    url,
                    cancellationToken);

                if (page?.Value is null)
                    throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "SAP did not return a production order list. The sync was stopped so no records are silently skipped.");

                if (page.Value.Count == 0)
                    break;

                foreach (var header in page.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (header.AbsoluteEntry is null or <= 0)
                        continue;

                    var existed = await db.ProductionOrders.AsNoTracking()
                        .AnyAsync(
                            x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == header.AbsoluteEntry.Value,
                            cancellationToken);

                    // Collection responses omit ProductionOrderLines — fetch the complete document.
                    var detail = await requestHandler.GetOrThrowAsync<SapProductionOrdersResponse>(
                        Constants.SapApiUrls.GetProductionOrders(header.AbsoluteEntry.Value.ToString()),
                        cancellationToken);

                    if (detail?.AbsoluteEntry is null)
                        throw new ApiErrorException(
                            BaseErrorCodes.ValidationFailed,
                            $"SAP returned no detail for production order {header.AbsoluteEntry}. "
                            + $"Sync stopped after {added + updated} record(s) so nothing is silently skipped.");

                    var names = await ResolveMasterNamesAsync([detail], cancellationToken);
                    await UpsertFromSapAsync(detail, names, cancellationToken);
                    lastAbsoluteEntry = header.AbsoluteEntry.Value;
                    if (existed)
                        updated++;
                    else
                        added++;

                    if (added + updated >= MaxRecordsPerBatch || stopwatch.Elapsed >= BatchTimeBudget)
                    {
                        hasMore = true;
                        break;
                    }
                }

                if (hasMore)
                    break;

                url = ResolveNextLink(page.ODataNextLink);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Records already upserted stay committed; record the failure so the status line
            // cannot keep showing the previous successful sync.
            var failure =
                $"Sync failed after {added + updated} record(s) ({added} added, {updated} updated): {ex.Message}";
            await SaveSyncStateAsync(syncedAt, added + updated, failure, cancellationToken, lastAbsoluteEntry);
            await WriteAuditAsync(mode, null, added, updated, false, failure, stopwatch, cancellationToken);

            Log.Error(
                ex,
                "Production order {Mode} sync failed for {CompanyDb} after added={Added}, updated={Updated}, pages={Pages}",
                mode,
                CompanyDb,
                added,
                updated,
                pages);
            throw;
        }

        var upserted = added + updated;
        var message = BuildSyncMessage(newOnly, hasMore, added, updated, pages, startEntry, lastAbsoluteEntry);

        await SaveSyncStateAsync(syncedAt, upserted, message, cancellationToken, lastAbsoluteEntry);
        await WriteAuditAsync(mode, null, added, updated, true, message, stopwatch, cancellationToken);

        Log.Information(
            "Production order {Mode} sync batch for {CompanyDb}: added={Added}, updated={Updated}, pages={Pages}, hasMore={HasMore}, lastEntry={LastEntry}, durationMs={DurationMs}",
            mode,
            CompanyDb,
            added,
            updated,
            pages,
            hasMore,
            lastAbsoluteEntry,
            stopwatch.ElapsedMilliseconds);

        return new ProductionOrderSyncResult(
            CompanyDb,
            UpsertedCount: upserted,
            PageCount: pages,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: mode,
            AddedCount: added,
            UpdatedCount: updated,
            HasMore: hasMore,
            LastAbsoluteEntry: lastAbsoluteEntry);
    }

    private async Task<int> GetMaxLocalAbsoluteEntryAsync(CancellationToken cancellationToken) =>
        await db.ProductionOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .Select(x => (int?)x.AbsoluteEntry)
            .MaxAsync(cancellationToken) ?? 0;

    private static string BuildSyncMessage(
        bool newOnly,
        bool hasMore,
        int added,
        int updated,
        int pages,
        int startEntry,
        int lastEntry)
    {
        var upserted = added + updated;

        if (hasMore)
            return $"Synced {upserted} production order(s) ({added} added, {updated} updated) up to entry {lastEntry}. More remaining.";

        if (newOnly)
            return upserted == 0
                ? $"No new production orders in SAP (local max entry {startEntry})."
                : $"Added {added} new production order(s) from SAP (after entry {startEntry}).";

        return $"Synced {upserted} production order(s) ({added} added, {updated} updated) across {pages} page(s).";
    }

    /// <summary>Only real ProductionOrders fields — SAP rejects the whole query on an unknown property.</summary>
    private static string BuildSyncStartUrl(int minAbsoluteEntryExclusive)
    {
        var filter = minAbsoluteEntryExclusive > 0
            ? $"&$filter=AbsoluteEntry gt {minAbsoluteEntryExclusive}"
            : string.Empty;
        return Constants.SapApiUrls.GetAllProductionOrders
            + $"?$select=AbsoluteEntry&$orderby=AbsoluteEntry&$top={HeaderPageSize}{filter}";
    }

    private static string? ResolveNextLink(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
            return null;

        if (nextLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return nextLink;

        var baseUrl = Constants.SapServiceLayerUrl.TrimEnd('/');

        if (nextLink.StartsWith(Constants.SapBaseUrl, StringComparison.OrdinalIgnoreCase))
            return baseUrl + nextLink;

        if (nextLink.StartsWith('/'))
            return baseUrl + nextLink;

        if (nextLink.StartsWith("ProductionOrders", StringComparison.OrdinalIgnoreCase))
            return baseUrl + Constants.SapBaseUrl + "/" + nextLink.TrimStart('/');

        return baseUrl + "/" + nextLink.TrimStart('/');
    }

    /// <summary>
    /// Resolves customer and project display names for a batch of SAP orders. The lookup is
    /// batched and cached, so a page costs at most one extra call per master type.
    /// </summary>
    public async Task<ResolvedMasterNames> ResolveMasterNamesAsync(
        IReadOnlyList<SapProductionOrdersResponse> orders,
        CancellationToken cancellationToken = default)
    {
        var cardCodes = Distinct(orders.Select(o => o.CustomerCode));
        var projectCodes = Distinct(orders
            .Where(o => string.IsNullOrWhiteSpace(o.ProjectName))
            .Select(o => o.Project));

        if (cardCodes.Count == 0 && projectCodes.Count == 0)
            return ResolvedMasterNames.Empty;

        try
        {
            var lookup = await masterDataService.LookupMasterDataAsync(
                new MasterLookupRequest { CardCodes = cardCodes, ProjectCodes = projectCodes },
                cancellationToken);
            return new ResolvedMasterNames(lookup.BusinessPartners, lookup.Projects);
        }
        catch (Exception ex)
        {
            // A missing display name must not abort the sync — codes are still mirrored.
            Log.Warning(ex, "Production order sync could not resolve master names for {CompanyDb}", CompanyDb);
            return ResolvedMasterNames.Empty;
        }
    }

    private static List<string> Distinct(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// If a Hangfire worker dies mid-job, Status can stay Running forever and the list UI treats
    /// the full sync as in progress. Expire stale Running jobs so sync can recover.
    /// </summary>
    private static readonly TimeSpan StaleFullSyncTimeout = TimeSpan.FromHours(2);

    public async Task<ProductionOrderSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default)
    {
        await RecoverStaleFullSyncIfNeededAsync(cancellationToken);

        var state = await db.ProductionOrderSyncStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
            return null;

        return new ProductionOrderSyncResult(
            state.CompanyDb,
            state.LastSyncedCount ?? 0,
            0,
            state.LastSyncedAtUtc ?? DateTime.MinValue,
            state.LastSyncMessage ?? string.Empty,
            Mode: "status",
            LastAbsoluteEntry: state.LastAbsoluteEntry,
            Status: string.IsNullOrWhiteSpace(state.Status) ? ProductionOrderSyncState.StatusIdle : state.Status,
            HangfireJobId: state.HangfireJobId,
            StartedAtUtc: state.StartedAtUtc);
    }

    private async Task RecoverStaleFullSyncIfNeededAsync(CancellationToken cancellationToken)
    {
        var state = await db.ProductionOrderSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
            return;
        if (!string.Equals(state.Status, ProductionOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            return;

        var started = state.StartedAtUtc ?? state.LastSyncedAtUtc;
        if (started is null || DateTime.UtcNow - started.Value < StaleFullSyncTimeout)
            return;

        state.Status = ProductionOrderSyncState.StatusFailed;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncMessage =
            $"Full sync marked failed: still Running after {StaleFullSyncTimeout.TotalHours:0}h "
            + $"(started {started:u}). The worker likely stopped without finishing.";

        if (db.Entry(state).State == EntityState.Detached)
            db.ProductionOrderSyncStates.Attach(state);
        db.Entry(state).Property(x => x.Status).IsModified = true;
        db.Entry(state).Property(x => x.LastSyncedAtUtc).IsModified = true;
        db.Entry(state).Property(x => x.LastSyncMessage).IsModified = true;

        await db.SaveChangesAsync(cancellationToken);
        Log.Warning(
            "Cleared stale production order full sync Running status for {CompanyDb} (started {StartedAtUtc})",
            CompanyDb,
            started);
    }

    /// <summary>Marks the company sync as Running. Returns false when one is already Running.</summary>
    public async Task<bool> TryBeginFullSyncJobAsync(
        string? hangfireJobId,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        if (string.Equals(state.Status, ProductionOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            return false;

        state.Status = ProductionOrderSyncState.StatusRunning;
        state.HangfireJobId = hangfireJobId;
        state.StartedAtUtc = DateTime.UtcNow;
        state.LastAbsoluteEntry = null;
        state.LastSyncMessage = "Sync job queued (fill entry gaps, import newer orders, refresh open orders).";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetFullSyncJobIdAsync(string hangfireJobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.HangfireJobId = hangfireJobId;
        if (!string.Equals(state.Status, ProductionOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = ProductionOrderSyncState.StatusRunning;
        state.StartedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFullSyncProgressAsync(
        ProductionOrderSyncResult batch,
        int totalAdded,
        int totalUpdated,
        int batchNumber,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = ProductionOrderSyncState.StatusRunning;
        state.LastSyncedAtUtc = batch.SyncedAtUtc;
        state.LastSyncedCount = totalAdded + totalUpdated;
        state.LastAbsoluteEntry = batch.LastAbsoluteEntry;
        state.LastSyncMessage = batch.HasMore
            ? $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} so far "
              + $"({totalAdded} added, {totalUpdated} updated) up to entry {batch.LastAbsoluteEntry}."
            : $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} "
              + $"({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncSucceededAsync(
        int totalAdded,
        int totalUpdated,
        int? lastAbsoluteEntry,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        var total = totalAdded + totalUpdated;
        state.Status = ProductionOrderSyncState.StatusSucceeded;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncedCount = total;
        state.LastAbsoluteEntry = lastAbsoluteEntry;
        state.LastSyncMessage = total == 0
            ? "Sync completed: no production order changes to import."
            : $"Sync completed: {total} production order(s) ({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncFailedAsync(string message, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = ProductionOrderSyncState.StatusFailed;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncMessage = Truncate(message, 2000);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductionOrderSyncState> GetOrCreateSyncStateAsync(CancellationToken cancellationToken)
    {
        var state = await db.ProductionOrderSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is not null)
            return state;

        state = new ProductionOrderSyncState
        {
            CompanyDb = CompanyDb,
            Status = ProductionOrderSyncState.StatusIdle,
        };
        db.ProductionOrderSyncStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    private async Task SaveSyncStateAsync(
        DateTime syncedAt,
        int count,
        string message,
        CancellationToken cancellationToken,
        int? lastAbsoluteEntry = null)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);

        state.LastSyncedAtUtc = syncedAt;
        state.LastSyncedCount = count;
        state.LastSyncMessage = Truncate(message, 2000);
        if (lastAbsoluteEntry is not null)
            state.LastAbsoluteEntry = lastAbsoluteEntry;

        // Do not clobber an in-flight Hangfire job status from synchronous batch endpoints.
        if (!string.Equals(state.Status, ProductionOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = ProductionOrderSyncState.StatusIdle;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Audit rows are shown to users, so a logging failure must never fail the sync.</summary>
    private async Task WriteAuditAsync(
        string mode,
        int? absoluteEntry,
        int added,
        int updated,
        bool succeeded,
        string message,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            db.ProductionOrderSyncLogs.Add(new ProductionOrderSyncLog
            {
                CompanyDb = CompanyDb,
                Mode = mode,
                AbsoluteEntry = absoluteEntry,
                UserId = httpContextAccessor.GetUserIdAsync(),
                UserName = Truncate(httpContextAccessor.HttpContext?.User?.Identity?.Name, 150),
                CorrelationId = Truncate(httpContextAccessor.HttpContext?.TraceIdentifier, 64),
                AddedCount = added,
                UpdatedCount = updated,
                Succeeded = succeeded,
                Message = Truncate(message, 2000),
                DurationMs = stopwatch.ElapsedMilliseconds,
                CreatedOn = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write production order sync audit row for {CompanyDb}", CompanyDb);
        }
    }

    public async Task<(IReadOnlyList<ProductionOrderSyncLog> Items, int TotalCount)> ListAuditAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = PaginationRequest.Normalize(request);
        return await db.ProductionOrderSyncLogs
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .OrderByDescending(x => x.Id)
            .ToPaginatedListAsync(normalized, cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>Display names resolved from master data, keyed by code (case-insensitive).</summary>
public sealed record ResolvedMasterNames(
    IReadOnlyDictionary<string, string> BusinessPartners,
    IReadOnlyDictionary<string, string> Projects)
{
    public static readonly ResolvedMasterNames Empty = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
