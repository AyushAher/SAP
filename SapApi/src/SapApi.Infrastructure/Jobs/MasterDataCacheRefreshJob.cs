using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using Serilog;

namespace SapApi.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that re-fetches SAP master data into the distributed cache before the 1-hour TTL
/// expires. Runs once per configured <see cref="SapCredentials.Accounts"/> entry so each company DB
/// gets its own warm cache namespace.
/// </summary>
public class MasterDataCacheRefreshJob(
    IHttpContextAccessor httpContextAccessor,
    ISapLoginService sapLogin,
    SapMasterDataService masterDataService,
    IOptions<SapCredentials> sapCredentials,
    IOptions<HangfireOptions> hangfireOptions)
{
    public const string RecurringJobId = "master-data-cache-refresh";

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var accounts = sapCredentials.Value.Accounts ?? [];
        if (accounts.Count == 0)
            throw new InvalidOperationException(
                "SapCredentials:Accounts is empty. Configure at least one account with Username, Password, and CompanyDb.");

        var hangfire = hangfireOptions.Value;
        var failures = new List<string>();

        foreach (var account in accounts)
        {
            try
            {
                await RefreshForAccountAsync(account, hangfire.ServiceUserId, cancellationToken);
            }
            catch (Exception ex)
            {
                var company = account.CompanyDb ?? "(missing CompanyDb)";
                Log.Error(ex, "Master-data cache refresh failed for {CompanyDb}", company);
                failures.Add($"{company}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Master-data cache refresh failed for {failures.Count}/{accounts.Count} company DB(s): "
                + string.Join("; ", failures));
    }

    private async Task RefreshForAccountAsync(
        SapCompanyCredential account,
        int serviceUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.Username))
            throw new InvalidOperationException("SapCredentials account Username is required.");

        if (string.IsNullOrWhiteSpace(account.CompanyDb))
            throw new InvalidOperationException("SapCredentials account CompanyDb is required.");

        var password = ResolvePassword(account);
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                $"SapCredentials password is required for CompanyDb '{account.CompanyDb}'. "
                + "Set SapCredentials__Accounts__N__Password (or SAP_PASSWORD / SAP_PASSWORD_{CompanyDb}).");

        var companyDb = ResolveCompanyDb(account.CompanyDb);

        // SapLoginService / CurrentCompanyDbAccessor / HttpRequestHandler all resolve identity from
        // HttpContext claims. Hangfire has no real request, so we install a synthetic one for the
        // duration of this company DB's refresh (AsyncLocal flows through awaited SAP calls).
        var previous = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = CreateServiceHttpContext(serviceUserId, companyDb);
        try
        {
            await EnsureSapSessionAsync(serviceUserId, account.Username!, password!, companyDb, cancellationToken);
            await masterDataService.WarmCacheAsync(cancellationToken);
            Log.Information(
                "Master-data cache refresh completed for {CompanyDb} using SAP user {SapUser}",
                companyDb,
                account.Username);
        }
        finally
        {
            httpContextAccessor.HttpContext = previous;
        }
    }

    private async Task EnsureSapSessionAsync(
        int serviceUserId,
        string userName,
        string password,
        SapCompanyDatabase companyDb,
        CancellationToken cancellationToken)
    {
        try
        {
            await sapLogin.SapLoginAsync(cancellationToken);
        }
        catch (ApiErrorException)
        {
            await sapLogin.LoginWithUserCredentialsAsync(serviceUserId, userName, password, companyDb, cancellationToken);
        }
    }

    /// <summary>
    /// Password resolution order: account.Password → SAP_PASSWORD_{CompanyDb} → SAP_PASSWORD → SAP_DEV_PASSWORD.
    /// </summary>
    public static string? ResolvePassword(SapCompanyCredential account)
    {
        if (!string.IsNullOrWhiteSpace(account.Password))
            return account.Password;

        if (!string.IsNullOrWhiteSpace(account.CompanyDb))
        {
            var perCompany = Environment.GetEnvironmentVariable($"SAP_PASSWORD_{account.CompanyDb.Trim().ToUpperInvariant()}");
            if (!string.IsNullOrWhiteSpace(perCompany))
                return perCompany;
        }

        return Environment.GetEnvironmentVariable("SAP_PASSWORD")
               ?? Environment.GetEnvironmentVariable("SAP_DEV_PASSWORD");
    }

    public static SapCompanyDatabase ResolveCompanyDb(string? configured) =>
        Enum.TryParse<SapCompanyDatabase>(configured, ignoreCase: true, out var companyDb)
            ? companyDb
            : throw new InvalidOperationException(
                $"Unknown SapCredentials CompanyDb '{configured}'. Expected one of: "
                + string.Join(", ", Enum.GetNames<SapCompanyDatabase>()));

    public static DefaultHttpContext CreateServiceHttpContext(int userId, SapCompanyDatabase companyDb)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(SapClaimTypes.CompanyDb, companyDb.ToString()),
        ],
        authenticationType: "Hangfire");

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
    }
}
