using SapApi.Domain.Models;
using SapApi.Shared.Enums;

namespace SapApi.Domain.Interfaces;

public interface ISapSessionStore
{
    Task<SapSessionInfo?> GetSessionAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);
    Task<SapRenewalCredentials?> GetCredentialsAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);
    Task SetSessionAsync(int userId, SapCompanyDatabase companyDb, SapSessionInfo session, SapRenewalCredentials credentials, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveSessionAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);
}
