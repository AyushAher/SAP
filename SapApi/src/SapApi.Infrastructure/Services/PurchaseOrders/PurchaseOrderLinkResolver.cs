using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.PurchaseOrders;

/// <summary>
/// Resolves the local <see cref="PurchaseOrder.Id"/> for payments/approvals from a SAP DocEntry.
/// </summary>
public class PurchaseOrderLinkResolver(
    AppDbContext db,
    ICurrentCompanyDbAccessor companyDbAccessor,
    PurchaseOrderLocalStore localStore)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public Task<int?> GetIdByDocEntryAsync(int docEntry, CancellationToken cancellationToken = default) =>
        db.PurchaseOrders.AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocEntry == docEntry)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> GetIdByDocNumAsync(int docNum, CancellationToken cancellationToken = default) =>
        db.PurchaseOrders.AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocNum == docNum)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Returns local PO Id for the given SAP DocEntry, upserting from SAP when missing.
    /// </summary>
    public async Task<int?> EnsureIdFromSapPoAsync(
        SapPurchaseOrdersResponse? po,
        CancellationToken cancellationToken = default)
    {
        if (po?.DocEntry is null or <= 0)
            return null;

        var existing = await GetIdByDocEntryAsync(po.DocEntry.Value, cancellationToken);
        if (existing is not null)
            return existing;

        await localStore.UpsertFromSapAsync(po, cancellationToken);
        return await GetIdByDocEntryAsync(po.DocEntry.Value, cancellationToken);
    }

    public async Task<int?> EnsureIdByDocEntryAsync(
        int docEntry,
        CancellationToken cancellationToken = default)
    {
        if (docEntry <= 0)
            return null;

        var existing = await GetIdByDocEntryAsync(docEntry, cancellationToken);
        if (existing is not null)
            return existing;

        await localStore.SyncOneFromSapAsync(docEntry, cancellationToken);
        return await GetIdByDocEntryAsync(docEntry, cancellationToken);
    }
}
