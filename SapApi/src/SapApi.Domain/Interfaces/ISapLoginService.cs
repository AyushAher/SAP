using SapApi.Shared.Enums;

namespace SapApi.Domain.Interfaces;

public interface ISapLoginService
{
    Task ValidateCredentialsAsync(string userName, string password, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);

    Task LoginWithUserCredentialsAsync(int userId, string userName, string password, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);

    Task<string?> GetSessionIdAsync(CancellationToken cancellationToken = default);

    Task SapLoginAsync(CancellationToken cancellationToken = default);

    Task RenewSessionAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default);
}
