using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using SapApi.Shared.Configuration;

namespace SapApi.Infrastructure.Services;

/// <summary>Acquires app-only Microsoft Graph access tokens via the client-credentials flow.
/// Split out from <see cref="GraphMailSender"/> so the HTTP/payload logic can be unit-tested
/// without exercising MSAL's own (hard-to-mock) fluent builder.</summary>
public interface IGraphAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public class GraphAccessTokenProvider(IOptions<GraphMailOptions> options) : IGraphAccessTokenProvider
{
    private readonly Lazy<IConfidentialClientApplication> app = new(() =>
    {
        var config = options.Value;
        return ConfidentialClientApplicationBuilder.Create(config.ClientId)
            .WithClientSecret(config.ClientSecret)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{config.TenantId}"))
            .Build();
    });

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var result = await app.Value
            .AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);
        return result.AccessToken;
    }
}
