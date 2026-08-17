using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Shared.Models;

namespace SapApi.Infrastructure.Services;

public class ActionAuditLogService(AppDbContext context) : IActionAuditLogService
{
    public async Task RecordAsync(ActionAuditEntry entry, CancellationToken cancellationToken = default)
    {
        context.ActionAuditLogs.Add(new ActionAuditLog
        {
            UserId = entry.UserId,
            UserName = Truncate(entry.UserName, 256),
            CompanyDb = Truncate(entry.CompanyDb, 64),
            HttpMethod = Truncate(entry.HttpMethod, 16) ?? string.Empty,
            Path = Truncate(entry.Path, 512) ?? string.Empty,
            Action = Truncate(entry.Action, 128) ?? string.Empty,
            StatusCode = entry.StatusCode,
            IpAddress = Truncate(entry.IpAddress, 64),
            DurationMs = entry.DurationMs,
            CreatedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginationResponse<List<ActionAuditLogListItem>>> ListAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = PaginationRequest.Normalize(request);
        var pageSize = normalized.PageSize ?? 20;
        var query = context.ActionAuditLogs.AsNoTracking();

        foreach (var filter in normalized.Filters)
        {
            var field = filter.Field.Trim();
            var value = filter.Value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value))
                continue;

            if (field.Equals("action", StringComparison.OrdinalIgnoreCase)
                && filter.Operator.Equals("contains", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Action.Contains(value));
                continue;
            }

            if (field.Equals("userName", StringComparison.OrdinalIgnoreCase)
                && filter.Operator.Equals("contains", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.UserName != null && x.UserName.Contains(value));
                continue;
            }

            if (field.Equals("path", StringComparison.OrdinalIgnoreCase)
                && filter.Operator.Equals("contains", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Path.Contains(value));
                continue;
            }

            if (field.Equals("companyDb", StringComparison.OrdinalIgnoreCase)
                && filter.Operator.Equals("eq", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.CompanyDb == value);
                continue;
            }

            if (field.Equals("statusCode", StringComparison.OrdinalIgnoreCase)
                && filter.Operator.Equals("eq", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var statusCode))
            {
                query = query.Where(x => x.StatusCode == statusCode);
            }
        }

        var totalCount = normalized.IncludeTotalCount
            ? await query.CountAsync(cancellationToken)
            : (int?)null;

        var sort = normalized.Sorts.FirstOrDefault();
        query = sort?.Field.ToLowerInvariant() switch
        {
            "action" => sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(x => x.Action)
                : query.OrderBy(x => x.Action),
            "username" => sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(x => x.UserName)
                : query.OrderBy(x => x.UserName),
            "statuscode" => sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(x => x.StatusCode)
                : query.OrderBy(x => x.StatusCode),
            _ => query.OrderByDescending(x => x.CreatedAt),
        };

        var rows = await query
            .Skip((normalized.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ActionAuditLogListItem
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                CompanyDb = x.CompanyDb,
                HttpMethod = x.HttpMethod,
                Path = x.Path,
                Action = x.Action,
                StatusCode = x.StatusCode,
                IpAddress = x.IpAddress,
                DurationMs = x.DurationMs,
                CreatedAt = x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PaginationResponse<List<ActionAuditLogListItem>>
        {
            Success = true,
            Data = rows,
            PageNumber = normalized.PageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            Filters = normalized.Filters,
            Sorts = normalized.Sorts,
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class ActionAuditLogListItem
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? CompanyDb { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
