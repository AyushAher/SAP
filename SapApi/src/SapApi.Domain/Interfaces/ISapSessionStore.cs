using SapApi.Domain.Models;

namespace SapApi.Domain.Interfaces;

public interface ISapSessionStore
{
    Task<SapSessionInfo?> GetSessionAsync(int userId, CancellationToken cancellationToken = default);
    Task<SapRenewalCredentials?> GetCredentialsAsync(int userId, CancellationToken cancellationToken = default);
    Task SetSessionAsync(int userId, SapSessionInfo session, SapRenewalCredentials credentials, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveSessionAsync(int userId, CancellationToken cancellationToken = default);
}
