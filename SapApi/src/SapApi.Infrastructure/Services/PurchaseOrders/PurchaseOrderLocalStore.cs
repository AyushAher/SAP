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
    int? DocEntry = null);

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

        if (request.Sorts.Count == 0)
            query = query.OrderByDescending(x => x.DocEntry);

        var (items, totalCount) = await query.ToPaginatedListAsync(request, cancellationToken);
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

        var detail = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
            Constants.SapApiUrls.UpdateSapPurchaseOrders(docEntry),
            cancellationToken: cancellationToken);

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
    /// Incremental sync: only DocEntries greater than the highest already stored locally.
    /// When the local table is empty, this imports all POs from SAP.
    /// </summary>
    public Task<PurchaseOrderSyncResult> SyncNewFromSapAsync(CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: true, cancellationToken);

    /// <summary>Full re-sync of every PO from SAP into the local table.</summary>
    public Task<PurchaseOrderSyncResult> SyncAllFromSapAsync(CancellationToken cancellationToken = default) =>
        SyncFromSapInternalAsync(newOnly: false, cancellationToken);

    private async Task<PurchaseOrderSyncResult> SyncFromSapInternalAsync(
        bool newOnly,
        CancellationToken cancellationToken)
    {
        var syncedAt = DateTime.UtcNow;
        var added = 0;
        var updated = 0;
        var pages = 0;

        var maxDocEntry = 0;
        if (newOnly)
        {
            maxDocEntry = await db.PurchaseOrders
                .AsNoTracking()
                .Where(x => x.CompanyDb == CompanyDb)
                .Select(x => (int?)x.DocEntry)
                .MaxAsync(cancellationToken) ?? 0;
        }

        var url = BuildSyncStartUrl(maxDocEntry);

        while (!string.IsNullOrWhiteSpace(url))
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages++;

            var page = await requestHandler.GetAsync<GetAllSapPurchaseOrdersResponse>(url, cancellationToken: cancellationToken);
            if (page?.Value is null || page.Value.Count == 0)
                break;

            foreach (var header in page.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (header.DocEntry is null or <= 0)
                    continue;

                var existed = await db.PurchaseOrders.AsNoTracking()
                    .AnyAsync(x => x.CompanyDb == CompanyDb && x.DocEntry == header.DocEntry.Value, cancellationToken);

                // Collection responses often omit DocumentLines / full UDFs — fetch complete doc.
                var detail = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseOrders(header.DocEntry),
                    cancellationToken: cancellationToken);

                if (detail?.DocEntry is null)
                {
                    Log.Warning("PO sync skipped DocEntry {DocEntry}: empty SAP detail", header.DocEntry);
                    continue;
                }

                await UpsertFromSapAsync(detail, cancellationToken);
                if (existed)
                    updated++;
                else
                    added++;
            }

            url = ResolveNextLink(page.ODataNextLink);
        }

        var upserted = added + updated;
        var mode = newOnly ? "new" : "full";
        var message = newOnly
            ? (upserted == 0
                ? $"No new purchase orders in SAP (local max DocEntry {maxDocEntry})."
                : $"Added {added} new purchase order(s) from SAP (after DocEntry {maxDocEntry}).")
            : $"Synced {upserted} purchase order(s) ({added} added, {updated} updated) across {pages} page(s).";

        await SaveSyncStateAsync(syncedAt, upserted, message, cancellationToken);

        Log.Information(
            "Purchase order {Mode} sync completed for {CompanyDb}: added={Added}, updated={Updated}, pages={Pages}",
            mode,
            CompanyDb,
            added,
            updated,
            pages);

        return new PurchaseOrderSyncResult(
            CompanyDb,
            UpsertedCount: upserted,
            PageCount: pages,
            SyncedAtUtc: syncedAt,
            Message: message,
            Mode: mode,
            AddedCount: added,
            UpdatedCount: updated);
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
            Mode: "status");
    }

    private async Task SaveSyncStateAsync(
        DateTime syncedAt,
        int count,
        string message,
        CancellationToken cancellationToken)
    {
        var state = await db.PurchaseOrderSyncStates
            .FirstOrDefaultAsync(x => x.CompanyDb == CompanyDb, cancellationToken);
        if (state is null)
        {
            state = new PurchaseOrderSyncState { CompanyDb = CompanyDb };
            db.PurchaseOrderSyncStates.Add(state);
        }

        state.LastSyncedAtUtc = syncedAt;
        state.LastSyncedCount = count;
        state.LastSyncMessage = message;
        await db.SaveChangesAsync(cancellationToken);
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
