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
using SapApi.Shared.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services.Items;

public record ItemSyncResult(
    string CompanyDb,
    int UpsertedCount,
    DateTime SyncedAtUtc,
    string Message,
    string Mode = "full",
    int AddedCount = 0,
    int UpdatedCount = 0,
    int PageCount = 0,
    /// <summary>True when the batch stopped early and the caller should sync again to continue.</summary>
    bool HasMore = false,
    /// <summary>Highest ItemCode processed — pass back as afterItemCode to resume. Items have no
    /// monotonic numeric key like DocEntry, so the cursor is the code itself (alphabetic order).</summary>
    string? LastItemCode = null,
    string Status = ItemSyncState.StatusIdle,
    string? HangfireJobId = null,
    DateTime? StartedAtUtc = null);

/// <summary>
/// Local mirror of the SAP Items (OITM) master, so item search/lookup never blocks on a live SAP
/// call. Unlike Purchase Orders, an Items list page already carries every field the app needs —
/// no per-row detail fetch, no DocEntry-style integer sequence to gap-fill.
/// </summary>
public class ItemLocalStore(
    AppDbContext db,
    IHttpRequestHandler requestHandler,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    /// <summary>Caps work per call so a reverse-proxy read timeout (nginx defaults to 60s) never
    /// kills an in-flight sync; the caller resumes with afterItemCode.</summary>
    private const int MaxRecordsPerBatch = 2000;

    private static readonly TimeSpan BatchTimeBudget = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan StaleFullSyncTimeout = TimeSpan.FromHours(2);

    public async Task<PaginationResponse<List<ItemsResponse>>> ListFromDbAsync(
        PaginationRequest request,
        IReadOnlyCollection<int>? groupCodes = null,
        CancellationToken cancellationToken = default)
    {
        // Caller resolved a group-name filter (e.g. "Consumable") to zero SAP group codes —
        // an unfiltered page here would silently ignore that filter instead of matching nothing.
        if (groupCodes is { Count: 0 })
        {
            var empty = PaginationRequest.Normalize(request);
            return PaginationResponseFactory.Create(empty, new List<ItemsResponse>(), 0);
        }

        var query = db.Items.AsNoTracking().Where(x => x.CompanyDb == CompanyDb);
        if (groupCodes is { Count: > 0 })
            query = query.Where(x => x.ItemsGroupCode != null && groupCodes.Contains(x.ItemsGroupCode.Value));

        var (queryWithFilters, remainingFilters) = ApplyItemListFilters(query, request.Filters);
        query = queryWithFilters;

        if (request.Sorts.Count == 0)
            query = query.OrderBy(x => x.ItemCode);

        var listRequest = new PaginationRequest
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Sorts = request.Sorts,
            Filters = remainingFilters,
        };

        var (items, totalCount) = await query.ToPaginatedListAsync(listRequest, cancellationToken);
        var data = items.Select(ItemMapper.ToSapResponse).ToList();
        return PaginationResponseFactory.Create(request, data, totalCount);
    }

    /// <summary>
    /// Mid-string search across ItemCode/ItemName (the "__search" convention every master-data
    /// search box sends), plus direct code/name contains filters for callers that target one column.
    /// </summary>
    private static (IQueryable<Item> Query, List<FilterModel> RemainingFilters) ApplyItemListFilters(
        IQueryable<Item> query,
        List<FilterModel> filters)
    {
        var remaining = new List<FilterModel>();

        foreach (var filter in filters)
        {
            if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                continue;

            var termLower = filter.Value.ToString()!.Trim().ToLowerInvariant();

            if (filter.Field.Equals("__search", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.ItemCode.ToLower().Contains(termLower)
                    || (x.ItemName != null && x.ItemName.ToLower().Contains(termLower)));
                continue;
            }

            if (filter.Field.Equals("ItemCode", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ItemCode.ToLower().Contains(termLower));
                continue;
            }

            if (filter.Field.Equals("ItemName", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ItemName != null && x.ItemName.ToLower().Contains(termLower));
                continue;
            }

            remaining.Add(filter);
        }

        return (query, remaining);
    }

    public async Task UpsertFromSapAsync(ItemsResponse sap, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sap.ItemCode))
            return;

        var now = DateTime.UtcNow;
        var entity = await db.Items
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb && x.ItemCode == sap.ItemCode, cancellationToken);

        if (entity is null)
        {
            entity = new Item { CompanyDb = CompanyDb, ItemCode = sap.ItemCode, CreatedOn = now };
            db.Items.Add(entity);
        }

        entity.IsDeleted = false;
        ItemMapper.ApplyFromSap(entity, sap, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Refresh a single item from SAP into the local table.</summary>
    public async Task<ItemSyncResult> SyncOneFromSapAsync(
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        var syncedAt = DateTime.UtcNow;
        var safeCode = SapPaginationBuilder.EscapeODataString(itemCode);
        var url = Constants.SapApiUrls.ItemsCollection
            + "?$select=ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM,InventoryWeight,"
            + $"PurchaseUnit,PurchaseItemsPerUnit,PurchaseVATGroup,ChapterID,DefaultWarehouse&$filter=ItemCode eq '{safeCode}'&$top=1";

        var response = await requestHandler.GetOrThrowAsync<SapItemsResponse>(url, cancellationToken);
        var item = response?.Value?.FirstOrDefault();
        if (item?.ItemCode is null)
        {
            var notFoundMessage = $"SAP has no item with code {itemCode}.";
            await SaveSyncStateAsync(syncedAt, 0, notFoundMessage, cancellationToken);
            return new ItemSyncResult(CompanyDb, 0, syncedAt, notFoundMessage, Mode: "one");
        }

        var existed = await db.Items.AsNoTracking()
            .AnyAsync(x => x.CompanyDb == CompanyDb && x.ItemCode == item.ItemCode, cancellationToken);
        await UpsertFromSapAsync(item, cancellationToken);

        var message = existed
            ? $"Refreshed {item.ItemCode} from SAP."
            : $"Imported {item.ItemCode} from SAP.";
        await SaveSyncStateAsync(syncedAt, 1, message, cancellationToken, item.ItemCode);

        return new ItemSyncResult(
            CompanyDb,
            UpsertedCount: 1,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: "one",
            AddedCount: existed ? 0 : 1,
            UpdatedCount: existed ? 1 : 0,
            LastItemCode: item.ItemCode);
    }

    /// <summary>Full (re)sync of the item catalog from SAP, ordered by ItemCode. Resumable via
    /// afterItemCode when a batch hits its record/time cap.</summary>
    public async Task<ItemSyncResult> SyncAllFromSapAsync(
        string? afterItemCode = null,
        CancellationToken cancellationToken = default)
    {
        var syncedAt = DateTime.UtcNow;
        var added = 0;
        var updated = 0;
        var pages = 0;
        var hasMore = false;
        var lastItemCode = afterItemCode;

        var url = BuildSyncStartUrl(afterItemCode);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (!string.IsNullOrWhiteSpace(url))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages++;

                // GetOrThrowAsync: a swallowed failure here would end the loop early and be
                // reported to the user as a successful sync that imported nothing.
                var page = await requestHandler.GetOrThrowAsync<SapItemsResponse>(url, cancellationToken);

                if (page?.Value is null)
                    throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "SAP did not return an item list. The sync was stopped so no records are silently skipped.");

                if (page.Value.Count == 0)
                    break;

                foreach (var item in page.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(item.ItemCode))
                        continue;

                    var existed = await db.Items.AsNoTracking()
                        .AnyAsync(x => x.CompanyDb == CompanyDb && x.ItemCode == item.ItemCode, cancellationToken);

                    await UpsertFromSapAsync(item, cancellationToken);
                    lastItemCode = item.ItemCode;
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
            var failureMessage =
                $"Item sync failed after {added + updated} record(s) ({added} added, {updated} updated): {ex.Message}";
            await SaveSyncStateAsync(syncedAt, added + updated, failureMessage, cancellationToken, lastItemCode);

            Log.Error(
                ex,
                "Item full sync failed for {CompanyDb} after added={Added}, updated={Updated}, pages={Pages}",
                CompanyDb,
                added,
                updated,
                pages);
            throw;
        }

        var upserted = added + updated;
        var message = hasMore
            ? $"Synced {upserted} item(s) ({added} added, {updated} updated) up to ItemCode {lastItemCode}. More remaining."
            : $"Synced {upserted} item(s) ({added} added, {updated} updated) across {pages} page(s).";

        await SaveSyncStateAsync(syncedAt, upserted, message, cancellationToken, lastItemCode);

        Log.Information(
            "Item full sync batch for {CompanyDb}: added={Added}, updated={Updated}, pages={Pages}, hasMore={HasMore}, lastItemCode={LastItemCode}",
            CompanyDb,
            added,
            updated,
            pages,
            hasMore,
            lastItemCode);

        return new ItemSyncResult(
            CompanyDb,
            UpsertedCount: upserted,
            SyncedAtUtc: syncedAt,
            Message: message,
            AddedCount: added,
            UpdatedCount: updated,
            PageCount: pages,
            HasMore: hasMore,
            LastItemCode: lastItemCode);
    }

    private static string BuildSyncStartUrl(string? afterItemCodeExclusive)
    {
        var filter = string.IsNullOrWhiteSpace(afterItemCodeExclusive)
            ? string.Empty
            : $"&$filter=ItemCode gt '{SapPaginationBuilder.EscapeODataString(afterItemCodeExclusive)}'";
        return Constants.SapApiUrls.ItemsCollection
            + "?$select=ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM,InventoryWeight,"
            + $"PurchaseUnit,PurchaseItemsPerUnit,PurchaseVATGroup,ChapterID,DefaultWarehouse&$orderby=ItemCode&$top=1000{filter}";
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

        if (nextLink.StartsWith("Items", StringComparison.OrdinalIgnoreCase))
            return baseUrl + Constants.SapBaseUrl + "/" + nextLink.TrimStart('/');

        return baseUrl + "/" + nextLink.TrimStart('/');
    }

    public async Task<ItemSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default)
    {
        await RecoverStaleFullSyncIfNeededAsync(cancellationToken);

        var state = await db.ItemSyncStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
            return null;

        return new ItemSyncResult(
            state.CompanyDb,
            state.LastSyncedCount ?? 0,
            state.LastSyncedAtUtc ?? DateTime.MinValue,
            state.LastSyncMessage ?? string.Empty,
            Mode: "status",
            LastItemCode: state.LastItemCode,
            Status: string.IsNullOrWhiteSpace(state.Status) ? ItemSyncState.StatusIdle : state.Status,
            HangfireJobId: state.HangfireJobId,
            StartedAtUtc: state.StartedAtUtc);
    }

    private async Task RecoverStaleFullSyncIfNeededAsync(CancellationToken cancellationToken)
    {
        var state = await db.ItemSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
            return;
        if (!string.Equals(state.Status, ItemSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            return;

        var started = state.StartedAtUtc ?? state.LastSyncedAtUtc;
        if (started is null || DateTime.UtcNow - started.Value < StaleFullSyncTimeout)
            return;

        state.Status = ItemSyncState.StatusFailed;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncMessage =
            $"Full sync marked failed: still Running after {StaleFullSyncTimeout.TotalHours:0}h "
            + $"(started {started:u}). The worker likely stopped without finishing.";

        if (db.Entry(state).State == EntityState.Detached)
            db.ItemSyncStates.Attach(state);
        db.Entry(state).Property(x => x.Status).IsModified = true;
        db.Entry(state).Property(x => x.LastSyncedAtUtc).IsModified = true;
        db.Entry(state).Property(x => x.LastSyncMessage).IsModified = true;

        await db.SaveChangesAsync(cancellationToken);
        Log.Warning("Cleared stale item full sync Running status for {CompanyDb} (started {StartedAtUtc})", CompanyDb, started);
    }

    public async Task<bool> TryBeginFullSyncJobAsync(
        string? hangfireJobId,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        if (string.Equals(state.Status, ItemSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            return false;

        state.Status = ItemSyncState.StatusRunning;
        state.HangfireJobId = hangfireJobId;
        state.StartedAtUtc = DateTime.UtcNow;
        state.LastItemCode = null;
        state.LastSyncMessage = "Sync job queued.";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetFullSyncJobIdAsync(string hangfireJobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.HangfireJobId = hangfireJobId;
        if (!string.Equals(state.Status, ItemSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = ItemSyncState.StatusRunning;
        state.StartedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFullSyncProgressAsync(
        ItemSyncResult batch,
        int totalAdded,
        int totalUpdated,
        int batchNumber,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = ItemSyncState.StatusRunning;
        state.LastSyncedAtUtc = batch.SyncedAtUtc;
        state.LastSyncedCount = totalAdded + totalUpdated;
        state.LastItemCode = batch.LastItemCode;
        state.LastSyncMessage = batch.HasMore
            ? $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} so far "
              + $"({totalAdded} added, {totalUpdated} updated) up to ItemCode {batch.LastItemCode}."
            : $"Running batch {batchNumber}: synced {totalAdded + totalUpdated} "
              + $"({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncSucceededAsync(
        int totalAdded,
        int totalUpdated,
        string? lastItemCode,
        CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        var total = totalAdded + totalUpdated;
        state.Status = ItemSyncState.StatusSucceeded;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncedCount = total;
        state.LastItemCode = lastItemCode;
        state.LastSyncMessage = total == 0
            ? "Sync completed: no items returned from SAP."
            : $"Sync completed: {total} item(s) ({totalAdded} added, {totalUpdated} updated).";
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFullSyncFailedAsync(string message, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);
        state.Status = ItemSyncState.StatusFailed;
        state.LastSyncedAtUtc = DateTime.UtcNow;
        state.LastSyncMessage = message.Length > 2000 ? message[..2000] : message;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ItemSyncState> GetOrCreateSyncStateAsync(CancellationToken cancellationToken)
    {
        var state = await db.ItemSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is not null)
            return state;

        state = new ItemSyncState { CompanyDb = CompanyDb, Status = ItemSyncState.StatusIdle };
        db.ItemSyncStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    private async Task SaveSyncStateAsync(
        DateTime syncedAt,
        int count,
        string message,
        CancellationToken cancellationToken,
        string? lastItemCode = null)
    {
        var state = await GetOrCreateSyncStateAsync(cancellationToken);

        state.LastSyncedAtUtc = syncedAt;
        state.LastSyncedCount = count;
        state.LastSyncMessage = message;
        if (lastItemCode is not null)
            state.LastItemCode = lastItemCode;

        // Do not clobber an in-flight Hangfire job status from synchronous batch endpoints.
        if (!string.Equals(state.Status, ItemSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
            state.Status = ItemSyncState.StatusIdle;

        await db.SaveChangesAsync(cancellationToken);
    }
}
