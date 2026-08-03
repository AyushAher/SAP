using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services.PurchaseOrders;

public record PurchaseOrderSyncResult(
    string CompanyDb,
    int UpsertedCount,
    int PageCount,
    DateTime SyncedAtUtc,
    string Message,
    string Mode = "full",
    int AddedCount = 0,
    int UpdatedCount = 0,
    int? DocEntry = null,
    /// <summary>True when the batch stopped early and the caller should sync again to continue.</summary>
    bool HasMore = false,
    /// <summary>Highest DocEntry processed — pass back as afterDocEntry to resume.</summary>
    int? LastDocEntry = null,
    string Status = PurchaseOrderSyncState.StatusIdle,
    string? HangfireJobId = null,
    DateTime? StartedAtUtc = null);

public class PurchaseOrderLocalStore(
    AppDbContext db,
    IHttpRequestHandler requestHandler,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<PaginationResponse<List<SapPurchaseOrdersResponse>>> ListFromDbAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = db.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb);

        var (queryWithFilters, remainingFilters) = ApplyPurchaseOrderListFilters(query, request.Filters);
        query = queryWithFilters;

        if (request.Sorts.Count == 0)
            query = query.OrderByDescending(x => x.DocEntry);

        var listRequest = new PaginationRequest
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Sorts = request.Sorts,
            Filters = remainingFilters,
        };

        var (items, totalCount) = await query.ToPaginatedListAsync(listRequest, cancellationToken);
        var data = items.Select(e => PurchaseOrderMapper.ToSapResponse(e, includeLines: false)).ToList();
        return PaginationResponseFactory.Create(request, data, totalCount);
    }

    public async Task<SapPurchaseOrdersResponse?> GetFromDbAsync(
        int docEntry,
        bool includeLines,
        CancellationToken cancellationToken = default)
    {
        var query = db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyDb == CompanyDb && x.DocEntry == docEntry);
        query = includeLines
            ? query.Include(x => x.Lines).Include(x => x.PaymentTerms)
            : query.Include(x => x.PaymentTerms);

        var entity = await query.FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : PurchaseOrderMapper.ToSapResponse(entity, includeLines);
    }

    public async Task UpsertFromSapAsync(SapPurchaseOrdersResponse sap, CancellationToken cancellationToken = default)
    {
        if (sap.DocEntry is null or <= 0)
            return;

        var now = DateTime.UtcNow;
        var entity = await db.PurchaseOrders
            .Include(x => x.Lines)
            .Include(x => x.PaymentTerms)
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb && x.DocEntry == sap.DocEntry.Value, cancellationToken);

        if (entity is null)
        {
            entity = new PurchaseOrder
            {
                CompanyDb = CompanyDb,
                DocEntry = sap.DocEntry.Value,
                CreatedOn = now,
            };
            db.PurchaseOrders.Add(entity);
            PurchaseOrderMapper.ApplyHeader(entity, sap, now);
            await db.SaveChangesAsync(cancellationToken);

            entity.Lines = PurchaseOrderMapper.MapLines(entity.Id, sap.DocumentLines);
            entity.PaymentTerms = PurchaseOrderMapper.MapPaymentTerms(entity.Id, sap);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        PurchaseOrderMapper.ApplyHeader(entity, sap, now);
        db.PurchaseOrderLines.RemoveRange(entity.Lines);
        db.PurchaseOrderPaymentTerms.RemoveRange(entity.PaymentTerms);
        entity.Lines = PurchaseOrderMapper.MapLines(entity.Id, sap.DocumentLines);
        entity.PaymentTerms = PurchaseOrderMapper.MapPaymentTerms(entity.Id, sap);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Pulls complete PO detail from SAP for one DocEntry and upserts the local row.
    /// </summary>
    public async Task<PurchaseOrderSyncResult> SyncOneFromSapAsync(
        int docEntry,
        CancellationToken cancellationToken = default)
    {
        if (docEntry <= 0)
            throw new ArgumentOutOfRangeException(nameof(docEntry));

        var existed = await db.PurchaseOrders.AsNoTracking()
            .AnyAsync(x => x.CompanyDb == CompanyDb && x.DocEntry == docEntry, cancellationToken);

        var detail = await requestHandler.GetOrThrowAsync<SapPurchaseOrdersResponse>(
            Constants.SapApiUrls.UpdateSapPurchaseOrders(docEntry),
            cancellationToken);

        if (detail?.DocEntry is null)
            throw new ApiErrorException(
                BaseErrorCodes.ValidationFailed,
                $"Purchase order DocEntry {docEntry} was not found in SAP.");

        await UpsertFromSapAsync(detail, cancellationToken);

        var syncedAt = DateTime.UtcNow;
        var added = existed ? 0 : 1;
        var updated = existed ? 1 : 0;
        var message = existed
            ? $"Updated purchase order {docEntry} from SAP."
            : $"Added purchase order {docEntry} from SAP.";

        Log.Information(
            "Purchase order row sync for {CompanyDb} DocEntry {DocEntry}: {Action}",
            CompanyDb,
            docEntry,
            existed ? "updated" : "added");

        return new PurchaseOrderSyncResult(
            CompanyDb,
            UpsertedCount: 1,
            PageCount: 1,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: "one",
            AddedCount: added,
            UpdatedCount: updated,
            DocEntry: docEntry);
    }

    /// <summary>
    /// Each purchase order needs its own SAP detail call, so an unbounded sync runs for minutes and
    /// is killed by the reverse proxy (nginx defaults to a 60s read timeout) with a 504. Work is
    /// therefore capped per request and the caller resumes with <c>afterDocEntry</c>.
    /// </summary>
    private const int MaxRecordsPerBatch = 400;

    private static readonly TimeSpan BatchTimeBudget = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Incremental sync: only DocEntries greater than the highest already stored locally.
    /// When the local table is empty, this imports all POs from SAP.
    /// </summary>
    public Task<PurchaseOrderSyncResult> SyncNewFromSapAsync(
        int? afterDocEntry = null,
        CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: true, afterDocEntry, cancellationToken);

    /// <summary>Full re-sync of every PO from SAP into the local table.</summary>
    public Task<PurchaseOrderSyncResult> SyncAllFromSapAsync(
        int? afterDocEntry = null,
        CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: false, afterDocEntry, cancellationToken);

    private async Task<PurchaseOrderSyncResult> SyncFromSapInternalAsync(
        bool newOnly,
        int? afterDocEntry,
        CancellationToken cancellationToken)
    {
        var syncedAt = DateTime.UtcNow;
        var added = 0;
        var updated = 0;
        var pages = 0;
        var hasMore = false;

        // Resume point: explicit cursor wins, otherwise incremental starts after the local max.
        var startDocEntry = afterDocEntry
            ?? (newOnly ? await GetMaxLocalDocEntryAsync(cancellationToken) : 0);
        var lastDocEntry = startDocEntry;

        var url = BuildSyncStartUrl(startDocEntry);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (!string.IsNullOrWhiteSpace(url))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages++;

                // GetOrThrowAsync: a swallowed failure here would end the loop early and be
                // reported to the user as a successful sync that imported nothing.
                var page = await requestHandler.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(url, cancellationToken);

                if (page?.Value is null)
                    throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "SAP did not return a purchase order list. The sync was stopped so no records are silently skipped.");

                if (page.Value.Count == 0)
                    break;

                foreach (var header in page.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (header.DocEntry is null or <= 0)
                        continue;

                    var existed = await db.PurchaseOrders.AsNoTracking()
                        .AnyAsync(x => x.CompanyDb == CompanyDb && x.DocEntry == header.DocEntry.Value, cancellationToken);

                    // Collection responses often omit DocumentLines / full UDFs — fetch complete doc.
                    var detail = await requestHandler.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                        Constants.SapApiUrls.UpdateSapPurchaseOrders(header.DocEntry),
                        cancellationToken);

                    if (detail?.DocEntry is null)
                        throw new ApiErrorException(
                            BaseErrorCodes.ValidationFailed,
                            $"SAP returned no detail for purchase order DocEntry {header.DocEntry}. "
                            + $"Sync stopped after {added + updated} record(s) so nothing is silently skipped.");

                    await UpsertFromSapAsync(detail, cancellationToken);
                    lastDocEntry = header.DocEntry.Value;
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
            var failureMessage =
                $"Sync failed after {added + updated} record(s) ({added} added, {updated} updated): {ex.Message}";
            await SaveSyncStateAsync(syncedAt, added + updated, failureMessage, cancellationToken, lastDocEntry);

            Log.Error(
                ex,
                "Purchase order {Mode} sync failed for {CompanyDb} after added={Added}, updated={Updated}, pages={Pages}",
                newOnly ? "new" : "full",
                CompanyDb,
                added,
                updated,
                pages);
            throw;
        }

        var upserted = added + updated;
        var mode = newOnly ? "new" : "full";
        var message = BuildSyncMessage(newOnly, hasMore, added, updated, pages, startDocEntry, lastDocEntry);

        await SaveSyncStateAsync(syncedAt, upserted, message, cancellationToken, lastDocEntry);

        Log.Information(
            "Purchase order {Mode} sync batch for {CompanyDb}: added={Added}, updated={Updated}, pages={Pages}, hasMore={HasMore}, lastDocEntry={LastDocEntry}",
            mode,
            CompanyDb,
            added,
            updated,
            pages,
            hasMore,
            lastDocEntry);

        return new PurchaseOrderSyncResult(
            CompanyDb,
            UpsertedCount: upserted,
            PageCount: pages,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: mode,
            AddedCount: added,
            UpdatedCount: updated,
            HasMore: hasMore,
            LastDocEntry: lastDocEntry);
    }

    private async Task<int> GetMaxLocalDocEntryAsync(CancellationToken cancellationToken) =>
        await db.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .Select(x => (int?)x.DocEntry)
            .MaxAsync(cancellationToken) ?? 0;

    private static string BuildSyncMessage(
        bool newOnly,
        bool hasMore,
        int added,
        int updated,
        int pages,
        int startDocEntry,
        int lastDocEntry)
    {
        var upserted = added + updated;

        if (hasMore)
            return $"Synced {upserted} purchase order(s) ({added} added, {updated} updated) up to DocEntry {lastDocEntry}. More remaining.";

        if (newOnly)
            return upserted == 0
                ? $"No new purchase orders in SAP (local max DocEntry {startDocEntry})."
                : $"Added {added} new purchase order(s) from SAP (after DocEntry {startDocEntry}).";

        return $"Synced {upserted} purchase order(s) ({added} added, {updated} updated) across {pages} page(s).";
    }

    public async Task<PurchaseOrderSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.PurchaseOrderSyncStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
            return null;

        return new PurchaseOrderSyncResult(
            state.CompanyDb,
            state.LastSyncedCount ?? 0,
            0,
            state.LastSyncedAtUtc ?? DateTime.MinValue,
            state.LastSyncMessage ?? string.Empty,
            Mode: "status",
            LastDocEntry: state.LastDocEntry,
            Status: string.IsNullOrWhiteSpace(state.Status) ? PurchaseOrderSyncState.StatusIdle : state.Status,
            HangfireJobId: state.HangfireJobId,
            StartedAtUtc: state.StartedAtUtc);
    }

    /// <summary>
    /// Marks the company sync as Running for a Hangfire full sync. Returns false if already Running.
    /// </summary>
    public async Task<bool> TryBeginFullSyncJobAsync(
        string? hangfireJobId,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        if (string.Equals(state.Status, PurchaseOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            return false;

        state.Status = PurchaseOrderSyncState.StatusRunning;
        state.HangfireJobId = hangfireJobId;
        state.StartedAtUtc = DateTime.UtcNow;
        state.LastDocEntry = null;
                state.LastSyncMessage = "Sync job queued (starting after latest local DocEntry).";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetFullSyncJobIdAsync(string hangfireJobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.HangfireJobId = hangfireJobId;
        if (!string.Equals(state.Status, PurchaseOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = PurchaseOrderSyncState.StatusRunning;
        if (state.StartedAtUtc is null)
            state.StartedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFullSyncProgressAsync(
        PurchaseOrderSyncResult batch,
        int totalAdded,
        int totalUpdated,
        int batchNumber,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = PurchaseOrderSyncState.StatusRunning;
        state.LastSyncedAtUtc = batch.SyncedAtUtc;
        state.LastSyncedCount = totalAdded + totalUpdated;
        state.LastDocEntry = batch.LastDocEntry;
        state.LastSyncMessage = batch.HasMore
            ? $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} so far "
              + $"({totalAdded} added, {totalUpdated} updated) up to DocEntry {batch.LastDocEntry}."
            : $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} "
              + $"({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncSucceededAsync(
        int totalAdded,
        int totalUpdated,
        int? lastDocEntry,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        var total = totalAdded + totalUpdated;
        state.Status = PurchaseOrderSyncState.StatusSucceeded;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncedCount = total;
        state.LastDocEntry = lastDocEntry;
        state.LastSyncMessage = total == 0
            ? "Sync completed: no new purchase orders after the latest local DocEntry."
            : $"Sync completed: {total} purchase order(s) ({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncFailedAsync(string message, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = PurchaseOrderSyncState.StatusFailed;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncMessage = message.Length > 2000 ? message[..2000] : message;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PurchaseOrderSyncState> GetOrCreateSyncStateAsync(CancellationToken cancellationToken)
    {
        var state = await db.PurchaseOrderSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is not null)
            return state;

        state = new PurchaseOrderSyncState
        {
            CompanyDb = CompanyDb,
            Status = PurchaseOrderSyncState.StatusIdle,
        };
        db.PurchaseOrderSyncStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    private async Task SaveSyncStateAsync(
        DateTime syncedAt,
        int count,
        string message,
        CancellationToken cancellationToken,
        int? lastDocEntry = null)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);

        state.LastSyncedAtUtc = syncedAt;
        state.LastSyncedCount = count;
        state.LastSyncMessage = message;
        if (lastDocEntry is not null)
            state.LastDocEntry = lastDocEntry;

        // Do not clobber an in-flight Hangfire job status from synchronous batch endpoints.
        if (!string.Equals(state.Status, PurchaseOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = PurchaseOrderSyncState.StatusIdle;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Business Partner column search matches CardCode or CardName so mid-name keywords work.
    /// </summary>
    private static (IQueryable<PurchaseOrder> Query, List<FilterModel> RemainingFilters) ApplyPurchaseOrderListFilters(
        IQueryable<PurchaseOrder> query,
        List<FilterModel> filters)
    {
        var remaining = new List<FilterModel>();

        foreach (var filter in filters)
        {
            if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                continue;

            if (!filter.Field.Equals("CardCode", StringComparison.OrdinalIgnoreCase))
            {
                remaining.Add(filter);
                continue;
            }

            var term = filter.Value.ToString()!.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.CardCode != null && x.CardCode.ToLower().Contains(term)) ||
                (x.CardName != null && x.CardName.ToLower().Contains(term)));
        }

        return (query, remaining);
    }

    private static string BuildSyncStartUrl(int minDocEntryExclusive)
    {
        var filter = minDocEntryExclusive > 0
            ? $"&$filter=DocEntry gt {minDocEntryExclusive}"
            : string.Empty;
        return Constants.SapApiUrls.GetAllSapPurchaseOrders
            + $"?$select=DocEntry&$orderby=DocEntry&$top=1000{filter}";
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

        // Relative to collection, e.g. "PurchaseOrders?$skiptoken=..."
        if (nextLink.StartsWith("PurchaseOrders", StringComparison.OrdinalIgnoreCase))
            return baseUrl + Constants.SapBaseUrl + "/" + nextLink.TrimStart('/');

        return baseUrl + "/" + nextLink.TrimStart('/');
    }
}
