namespace SapApi.Domain.Interfaces;

public interface IActionAuditLogService
{
    Task RecordAsync(ActionAuditEntry entry, CancellationToken cancellationToken = default);
}

public sealed record ActionAuditEntry(
    int? UserId,
    string? UserName,
    string? CompanyDb,
    string HttpMethod,
    string Path,
    string Action,
    int StatusCode,
    string? IpAddress,
    int DurationMs);
