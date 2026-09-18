using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Configuration;

namespace SapApi.Infrastructure.Services;

/// <summary>
/// Sends mail via Microsoft Graph's /users/{mailbox}/sendMail, authenticating app-only
/// (client-credentials — no signed-in user) so a background job can send without anyone logged
/// in. Replaces SMTP AUTH, which Microsoft 365 is phasing out tenant by tenant.
/// </summary>
public class GraphMailSender(
    HttpClient http,
    IGraphAccessTokenProvider tokenProvider,
    IOptions<GraphMailOptions> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        RequireConfigured(config);

        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(config.SenderMailbox)}/sendMail")
        {
            Content = JsonContent.Create(BuildPayload(message)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Graph sendMail failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
    }

    internal static object BuildPayload(MailMessage message) => new
    {
        message = new
        {
            subject = message.Subject,
            body = new
            {
                contentType = message.IsHtml ? "HTML" : "Text",
                content = message.Body,
            },
            toRecipients = message.To.Select(ToRecipient).ToArray(),
            ccRecipients = message.Cc.Select(ToRecipient).ToArray(),
        },
        saveToSentItems = false,
    };

    private static object ToRecipient(string address) => new { emailAddress = new { address } };

    private static void RequireConfigured(GraphMailOptions config)
    {
        if (string.IsNullOrWhiteSpace(config.TenantId)
            || string.IsNullOrWhiteSpace(config.ClientId)
            || string.IsNullOrWhiteSpace(config.ClientSecret)
            || string.IsNullOrWhiteSpace(config.SenderMailbox))
        {
            throw new InvalidOperationException(
                "GraphMail is not configured. Set GraphMail:TenantId/ClientId/SenderMailbox in " +
                "appsettings.json and the GRAPH_CLIENT_SECRET environment variable at deploy time.");
        }
    }
}
