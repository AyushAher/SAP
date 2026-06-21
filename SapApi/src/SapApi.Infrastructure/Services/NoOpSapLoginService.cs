using SapApi.Domain.Interfaces;
using SapApi.Shared.Enums;

namespace SapApi.Infrastructure.Services;

public class NoOpSapLoginService : ISapLoginService
{
    public Task ValidateCredentialsAsync(string userName, string password, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task LoginWithUserCredentialsAsync(int userId, string userName, string password, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> GetSessionIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("test-session");

    public Task SapLoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RenewSessionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogoutAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
