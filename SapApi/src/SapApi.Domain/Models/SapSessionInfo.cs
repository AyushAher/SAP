namespace SapApi.Domain.Models;

public class SapSessionInfo
{
    public required string SessionId { get; init; }
    public required string UserName { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}
