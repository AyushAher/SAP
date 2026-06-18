using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Domain.Models;
using SapApi.Infrastructure.Identity;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services;

public class SapLoginService(
    ISapSessionStore sessionStore,
    IHttpContextAccessor httpContextAccessor,
    IAesEncryptionService aesEncryption,
    IOptions<SapCredentials> sapCredentials) : ISapLoginService
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> UserLocks = new();

    public async Task ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateWithSapAsync(userName, password, cancellationToken);
        try
        {
            await LogoutFromSapAsync(session.SessionId!, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SAP logout after credential validation failed for user {UserName}", userName);
        }
    }

    public async Task LoginWithUserCredentialsAsync(int userId, string userName, string password, CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateWithSapAsync(userName, password, cancellationToken);
        await StoreSessionAsync(userId, userName, password, session, cancellationToken);
        Log.Information("SAP session established for user {UserId}", userId);
    }

    public async Task<string?> GetSessionIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return null;

        var session = await TryGetValidSessionAsync(userId.Value, cancellationToken);
        return session?.SessionId;
    }

    public async Task SapLoginAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId()
            ?? throw new ApiErrorException(BaseErrorCodes.IncorrectCredentials, "SAP session not found. Please log in again.");

        if (await TryGetValidSessionAsync(userId, cancellationToken) is not null)
            return;

        await RenewSessionAsync(cancellationToken);
    }

    public async Task RenewSessionAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId()
            ?? throw new ApiErrorException(BaseErrorCodes.IncorrectCredentials, "SAP session expired. Please log in again.");

        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);
        try
        {
            if (await TryGetValidSessionAsync(userId, cancellationToken) is not null)
                return;

            var credentials = await sessionStore.GetCredentialsAsync(userId, cancellationToken);
            if (credentials is null)
                throw new ApiErrorException(BaseErrorCodes.IncorrectCredentials, "SAP session expired. Please log in again.");

            var password = aesEncryption.Decrypt(credentials.EncryptedPassword);
            var session = await AuthenticateWithSapAsync(credentials.UserName, password, cancellationToken);
            await StoreSessionAsync(userId, credentials.UserName, password, session, cancellationToken);
            Log.Information("SAP session renewed for user {UserId}", userId);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return;

        var session = await sessionStore.GetSessionAsync(userId.Value, cancellationToken);
        if (session is not null)
        {
            try
            {
                await LogoutFromSapAsync(session.SessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SAP logout failed for user {UserId}", userId);
            }
        }

        await sessionStore.RemoveSessionAsync(userId.Value, cancellationToken);
        Log.Information("SAP session cleared for user {UserId}", userId);
    }

    private int? GetCurrentUserId() => httpContextAccessor.GetUserIdAsync();

    private async Task<SapSessionInfo?> TryGetValidSessionAsync(int userId, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(userId, cancellationToken);
        if (session is null)
            return null;

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await sessionStore.RemoveSessionAsync(userId, cancellationToken);
            return null;
        }

        return session;
    }

    private async Task StoreSessionAsync(int userId, string userName, string password, SapLoginResponse sapResponse, CancellationToken cancellationToken)
    {
        var timeoutMinutes = sapResponse.SessionTimeout ?? 30;
        var ttl = TimeSpan.FromMinutes(Math.Max(timeoutMinutes - 1, 1));

        var session = new SapSessionInfo
        {
            SessionId = sapResponse.SessionId!,
            UserName = userName,
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl)
        };

        await sessionStore.SetSessionAsync(userId, session, new SapRenewalCredentials
        {
            UserName = userName,
            EncryptedPassword = aesEncryption.Encrypt(password)
        }, ttl, cancellationToken);
    }

    private async Task<SapLoginResponse> AuthenticateWithSapAsync(string userName, string password, CancellationToken cancellationToken)
    {
        using var client = CreateSapHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Constants.SapApiUrls.Login)
        {
            Content = new StringContent(JsonSerializer.Serialize(new SapLoginRequest
            {
                CompanyDB = sapCredentials.Value.CompanyDb ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "SAP company database is not configured"),
                Password = password,
                UserName = userName
            }), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var sapResponse = JsonSerializer.Deserialize<SapLoginResponse>(body);

        if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(sapResponse?.SessionId))
            throw new ApiErrorException(BaseErrorCodes.IncorrectCredentials, MapSapLoginError(sapResponse));

        return sapResponse;
    }

    private async Task LogoutFromSapAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var client = CreateSapHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Constants.SapApiUrls.Logout);
        request.Headers.Add("Cookie", $"B1SESSION={sessionId};");
        await client.SendAsync(request, cancellationToken);
    }

    private static HttpClient CreateSapHttpClient() => new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    private static string MapSapLoginError(SapLoginResponse? sapResponse)
    {
        var sapMessage = sapResponse?.Error?.Message?.Value;
        if (!string.IsNullOrWhiteSpace(sapMessage))
        {
            if (sapMessage.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
                || sapMessage.Contains("password", StringComparison.OrdinalIgnoreCase)
                || sapMessage.Contains("user", StringComparison.OrdinalIgnoreCase))
            {
                return "SAP credentials are invalid. Please verify your username and password.";
            }

            return "Unable to authenticate with SAP. Please try again.";
        }

        return "Unable to authenticate with SAP. Please verify your SAP credentials.";
    }
}
