using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public static class ProductionRequestMapper
{
    public static SapInventoryGenExitRequestOrderLines? ParseOrderLines(string? requestBody) =>
        string.IsNullOrWhiteSpace(requestBody)
            ? null
            : JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderLines>(requestBody);

    public static IssueForProductionRequests ToIssueEntity(SapInventoryGenExitRequestOrderLines orderLines, string companyDb)
    {
        var po = orderLines.ProductionOrder
            ?? throw new ArgumentException("Production order is required.");

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
        };
    }

    public static ReceiptFromProductionRequests ToReceiptEntity(SapInventoryGenExitRequestOrderLines orderLines, string companyDb)
    {
        var po = orderLines.ProductionOrder
            ?? throw new ArgumentException("Production order is required.");

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
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<(IReadOnlyList<IssueForProductionRequests> Items, int TotalCount)> ListAsync(
        PaginationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        return await db.IssueForProductionRequests.AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb)
            .OrderByDescending(x => x.Id)
            .ToPaginatedListAsync(normalized, cancellationToken);
    }

    public Task<IssueForProductionRequests?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.IssueForProductionRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);

    public async Task<IssueForProductionRequests> SaveAsync(
        SapInventoryGenExitRequestOrderLines orderLines,
        int? id,
        CancellationToken cancellationToken)
    {
        if (id is null or <= 0)
        {
            var entity = ProductionRequestMapper.ToIssueEntity(orderLines, CompanyDb);
            await db.IssueForProductionRequests.AddAsync(entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        var existing = await db.IssueForProductionRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken)
            ?? throw new KeyNotFoundException("Issue for production request not found.");

        var mapped = ProductionRequestMapper.ToIssueEntity(orderLines, CompanyDb);
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
