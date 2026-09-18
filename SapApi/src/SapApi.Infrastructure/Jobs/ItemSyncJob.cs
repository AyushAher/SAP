using Hangfire;
using Hangfire.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Items;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using Serilog;

namespace SapApi.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that fully syncs the SAP item master into Postgres for one company DB, renewing
/// the SAP session between batches until complete. Mirrors <see cref="PurchaseOrderSyncJob"/>,
/// minus the gap-fill phase (items have no monotonic key to develop holes in) and the per-record
/// detail fetch (the Items list response already carries every field the app needs).
/// </summary>
public class ItemSyncJob(
    IHttpContextAccessor httpContextAccessor,
    ISapLoginService sapLogin,
    ItemLocalStore localStore,
    IOptions<SapCredentials> sapCredentials,
    IOptions<HangfireOptions> hangfireOptions)
{
    public const string JobName = "item-full-sync";
    public const string RecurringJobId = "item-catalog-sync";

    /// <summary>
    /// Recurring-cron entry point: syncs every configured <see cref="SapCredentials.Accounts"/>
    /// company DB in turn (mirrors <see cref="MasterDataCacheRefreshJob.ExecuteAsync"/>), since a
    /// cron firing has no single requesting user/company the way a UI-triggered sync does.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var accounts = sapCredentials.Value.Accounts ?? [];
        if (accounts.Count == 0)
            throw new InvalidOperationException(
                "SapCredentials:Accounts is empty. Configure at least one account with Username, Password, and CompanyDb.");

        var serviceUserId = hangfireOptions.Value.ServiceUserId;
        var failures = new List<string>();

        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.CompanyDb))
                continue;

            try
            {
                await ExecuteAsync(account.CompanyDb, serviceUserId, performContext: null, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Item catalog sync failed for {CompanyDb}", account.CompanyDb);
                failures.Add($"{account.CompanyDb}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Item catalog sync failed for {failures.Count}/{accounts.Count} company DB(s): "
                + string.Join("; ", failures));
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        string companyDb,
        int requestingUserId,
        PerformContext? performContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyDb))
            throw new InvalidOperationException("companyDb is required for item full sync.");

        var resolvedDb = MasterDataCacheRefreshJob.ResolveCompanyDb(companyDb);
        var sessionUserId = requestingUserId > 0
            ? requestingUserId
            : hangfireOptions.Value.ServiceUserId;
        var hangfireJobId = performContext?.BackgroundJob?.Id;

        var previous = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext =
            MasterDataCacheRefreshJob.CreateServiceHttpContext(sessionUserId, resolvedDb);

        try
        {
            if (!string.IsNullOrWhiteSpace(hangfireJobId))
                await localStore.SetFullSyncJobIdAsync(hangfireJobId, cancellationToken);

            await EnsureSapSessionAsync(sessionUserId, companyDb, resolvedDb, cancellationToken);
            await RunFullSyncBatchesAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            Log.Warning(ex, "Item full sync cancelled for {CompanyDb}", companyDb);
            try
            {
                await localStore.MarkFullSyncFailedAsync("Full sync cancelled before completion.", CancellationToken.None);
            }
            catch (Exception markEx)
            {
                Log.Error(markEx, "Failed to persist Failed status after cancel for {CompanyDb}", companyDb);
            }

            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Item full sync failed for {CompanyDb}", companyDb);
            try
            {
                await localStore.MarkFullSyncFailedAsync($"Full sync failed: {ex.Message}", CancellationToken.None);
            }
            catch (Exception markEx)
            {
                Log.Error(markEx, "Failed to persist Failed status for {CompanyDb}", companyDb);
            }

            throw;
        }
        finally
        {
            httpContextAccessor.HttpContext = previous;
        }
    }

    private async Task RunFullSyncBatchesAsync(CancellationToken cancellationToken)
    {
        var batch = 0;
        var totalAdded = 0;
        var totalUpdated = 0;
        string? lastItemCode = null;
        string? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch++;
            await sapLogin.SapLoginAsync(cancellationToken);

            var result = await localStore.SyncAllFromSapAsync(cursor, cancellationToken);
            totalAdded += result.AddedCount;
            totalUpdated += result.UpdatedCount;
            lastItemCode = result.LastItemCode ?? lastItemCode;

            await localStore.UpdateFullSyncProgressAsync(result, totalAdded, totalUpdated, batch, cancellationToken);

            Log.Information(
                "Item sync batch {Batch} for {CompanyDb}: added={Added}, updated={Updated}, hasMore={HasMore}, lastItemCode={LastItemCode}",
                batch,
                result.CompanyDb,
                result.AddedCount,
                result.UpdatedCount,
                result.HasMore,
                result.LastItemCode);

            if (!result.HasMore)
                break;

            if (result.LastItemCode is null)
            {
                Log.Warning("Item sync reported HasMore but no LastItemCode for {CompanyDb}; stopping.", result.CompanyDb);
                break;
            }

            cursor = result.LastItemCode;
        }

        await localStore.MarkFullSyncSucceededAsync(totalAdded, totalUpdated, lastItemCode, cancellationToken);
        Log.Information(
            "Item sync completed for company: added={Added}, updated={Updated}, batches={Batches}",
            totalAdded,
            totalUpdated,
            batch);
    }

    private async Task EnsureSapSessionAsync(
        int sessionUserId,
        string companyDbName,
        SapCompanyDatabase companyDb,
        CancellationToken cancellationToken)
    {
        try
        {
            await sapLogin.SapLoginAsync(cancellationToken);
            Log.Information(
                "Item full sync using cached SAP session for user {UserId} on {CompanyDb}",
                sessionUserId,
                companyDb);
            return;
        }
        catch (ApiErrorException ex)
        {
            Log.Information(
                ex,
                "No reusable SAP session for user {UserId} on {CompanyDb}; trying service credentials",
                sessionUserId,
                companyDb);
        }

        try
        {
            var (userName, password) = MasterDataCacheRefreshJob.ResolveServiceLogin(
                sapCredentials.Value.Accounts,
                companyDbName);
            await sapLogin.LoginWithUserCredentialsAsync(
                sessionUserId,
                userName,
                password,
                companyDb,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"No SAP session for user {sessionUserId} on {companyDbName}, and service credentials are not configured. "
                + "Stay logged into the app (so a SAP session is cached), or set SAP_PASSWORD / "
                + "SapCredentials:Accounts:N:Password. "
                + ex.Message,
                ex);
        }
    }
}
