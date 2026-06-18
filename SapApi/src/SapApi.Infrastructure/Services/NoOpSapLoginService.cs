using SapApi.Domain.Interfaces;

namespace SapApi.Infrastructure.Services;

public class NoOpSapLoginService : ISapLoginService
{
    public Task ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task LoginWithUserCredentialsAsync(int userId, string userName, string password, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> GetSessionIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("test-session");

    public Task SapLoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RenewSessionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
