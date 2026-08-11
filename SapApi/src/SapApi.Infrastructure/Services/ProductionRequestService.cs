using System.Text.Json;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Infrastructure.Services;

public static class ProductionRequestMapper
{
    public static SapInventoryGenExitRequestOrderLines? ParseOrderLines(string? requestBody) =>
        string.IsNullOrWhiteSpace(requestBody)
            ? null
            : JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderLines>(requestBody);

    /// <summary>
    /// Backfills blank production-order header fields from the persisted draft row
    /// (older RequestBody payloads may omit ProjectName / CustomerName).
    /// </summary>
    public static SapInventoryGenExitRequestOrderLines? EnrichOrderLinesFromDraft(
        SapInventoryGenExitRequestOrderLines? orderLines,
        string? project,
        string? projectName,
        string? cardCode,
        string? cardName,
        string? status,
        string? itemNo,
        string? itemName,
        string? workerName = null)
    {
        if (orderLines is null) return orderLines;

        if (string.IsNullOrWhiteSpace(orderLines.WorkerName) && !string.IsNullOrWhiteSpace(workerName))
            orderLines.WorkerName = workerName;

        if (orderLines.ProductionOrder is null) return orderLines;

        var po = orderLines.ProductionOrder;
        if (string.IsNullOrWhiteSpace(po.Project) && !string.IsNullOrWhiteSpace(project))
            po.Project = project;
        if (string.IsNullOrWhiteSpace(po.ProjectName) && !string.IsNullOrWhiteSpace(projectName))
            po.ProjectName = projectName;
        if (string.IsNullOrWhiteSpace(po.CustomerCode) && !string.IsNullOrWhiteSpace(cardCode))
            po.CustomerCode = cardCode;
        if (string.IsNullOrWhiteSpace(po.CustomerName) && !string.IsNullOrWhiteSpace(cardName))
            po.CustomerName = cardName;
        if (string.IsNullOrWhiteSpace(po.Status) && !string.IsNullOrWhiteSpace(status))
            po.Status = status;
        if (string.IsNullOrWhiteSpace(po.ItemNumber) && !string.IsNullOrWhiteSpace(itemNo))
            po.ItemNumber = itemNo;
        if (string.IsNullOrWhiteSpace(po.ProductDescription) && !string.IsNullOrWhiteSpace(itemName))
            po.ProductDescription = itemName;

        return orderLines;
    }

    /// <summary>
    /// Production order required; lines may be empty so items can be added afterwards.
    /// When lines are present, IssuedQuantity ≤ PlannedQuantity.
    /// </summary>
    public static void ValidateForSave(SapInventoryGenExitRequestOrderLines orderLines)
    {
        if (orderLines.ProductionOrder is null)
            throw new ArgumentException("Production order is required.");

        var lines = orderLines.ProductionOrderLinesEntryNumber ?? [];
        if (lines.Any(x => x.IssuedQuantity > x.PlannedQuantity))
            throw new ArgumentException("Issued quantity cannot exceed planned quantity for any line item.");
    }

    public static IssueForProductionRequests ToIssueEntity(
        SapInventoryGenExitRequestOrderLines orderLines,
        string companyDb,
        string? createdByUserName = null)
    {
        ValidateForSave(orderLines);
        var po = orderLines.ProductionOrder!;

        return new IssueForProductionRequests
        {
            CompanyDb = companyDb,
            RequestBody = JsonSerializer.Serialize(orderLines),
            CardCode = po.CustomerCode ?? string.Empty,
            CardName = po.CustomerName ?? string.Empty,
            Project = po.Project ?? string.Empty,
            ProjectName = po.ProjectName ?? string.Empty,
            Status = po.Status ?? string.Empty,
            ItemNo = po.ItemNumber ?? string.Empty,
            ItemName = po.ProductDescription ?? string.Empty,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedByUserName = createdByUserName ?? string.Empty,
            WorkerName = orderLines.WorkerName ?? string.Empty,
        };
    }

    public static ReceiptFromProductionRequests ToReceiptEntity(SapInventoryGenExitRequestOrderLines orderLines, string companyDb)
    {
        ValidateForSave(orderLines);
        var po = orderLines.ProductionOrder!;

        return new ReceiptFromProductionRequests
        {
            CompanyDb = companyDb,
            RequestBody = JsonSerializer.Serialize(orderLines),
            CardCode = po.CustomerCode ?? string.Empty,
            CardName = po.CustomerName ?? string.Empty,
            Project = po.Project ?? string.Empty,
            ProjectName = po.ProjectName ?? string.Empty,
            Status = po.Status ?? string.Empty,
            ItemNo = po.ItemNumber ?? string.Empty,
            ItemName = po.ProductDescription ?? string.Empty,
        };
    }
}

public sealed class IssueForProductionService(AppDbContext db, ICurrentCompanyDbAccessor companyDbAccessor)
{
    private static readonly Dictionary<string, string[]> ListOrFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["itemNo"] = ["ItemNo", "ItemName"],
        ["itemName"] = ["ItemNo", "ItemName"],
        ["cardCode"] = ["CardCode", "CardName"],
        ["cardName"] = ["CardCode", "CardName"],
        ["createdByUserName"] = ["CreatedByUserName"],
        ["userName"] = ["CreatedByUserName"],
    };

    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<(IReadOnlyList<IssueForProductionRequests> Items, int TotalCount)> ListAsync(
        PaginationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        return await db.IssueForProductionRequests.AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .OrderByDescending(x => x.Id)
            .ToPaginatedListAsync(normalized, cancellationToken, ListOrFieldAliases);
    }

    public Task<IssueForProductionRequests?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.IssueForProductionRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);

    public async Task<IssueForProductionRequests> SaveAsync(
        SapInventoryGenExitRequestOrderLines orderLines,
        int? id,
        string? createdByUserName,
        CancellationToken cancellationToken)
    {
        if (id is null or <= 0)
        {
            var entity = ProductionRequestMapper.ToIssueEntity(orderLines, CompanyDb, createdByUserName);
            await db.IssueForProductionRequests.AddAsync(entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        var existing = await db.IssueForProductionRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken)
            ?? throw new KeyNotFoundException("Issue for production request not found.");

        var mapped = ProductionRequestMapper.ToIssueEntity(orderLines, CompanyDb, createdByUserName);
        existing.RequestBody = mapped.RequestBody;
        existing.CardCode = mapped.CardCode;
        existing.CardName = mapped.CardName;
        existing.Project = mapped.Project;
        existing.ProjectName = mapped.ProjectName;
        existing.Status = mapped.Status;
        existing.ItemNo = mapped.ItemNo;
        existing.ItemName = mapped.ItemName;
        existing.WorkerName = mapped.WorkerName;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var existing = await db.IssueForProductionRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);
        if (existing is null) return false;
        db.IssueForProductionRequests.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class ReceiptFromProductionService(AppDbContext db, ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<(IReadOnlyList<ReceiptFromProductionRequests> Items, int TotalCount)> ListAsync(
        PaginationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        return await db.ReceiptFromProductionRequests.AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .OrderByDescending(x => x.Id)
            .ToPaginatedListAsync(normalized, cancellationToken);
    }

    public Task<ReceiptFromProductionRequests?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.ReceiptFromProductionRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);

    public async Task<ReceiptFromProductionRequests> SaveAsync(
        SapInventoryGenExitRequestOrderLines orderLines,
        int? id,
        CancellationToken cancellationToken)
    {
        if (id is null or <= 0)
        {
            var entity = ProductionRequestMapper.ToReceiptEntity(orderLines, CompanyDb);
            await db.ReceiptFromProductionRequests.AddAsync(entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        var existing = await db.ReceiptFromProductionRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken)
            ?? throw new KeyNotFoundException("Receipt from production request not found.");

        var mapped = ProductionRequestMapper.ToReceiptEntity(orderLines, CompanyDb);
        existing.RequestBody = mapped.RequestBody;
        existing.CardCode = mapped.CardCode;
        existing.CardName = mapped.CardName;
        existing.Project = mapped.Project;
        existing.ProjectName = mapped.ProjectName;
        existing.Status = mapped.Status;
        existing.ItemNo = mapped.ItemNo;
        existing.ItemName = mapped.ItemName;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var existing = await db.ReceiptFromProductionRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);
        if (existing is null) return false;
        db.ReceiptFromProductionRequests.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
