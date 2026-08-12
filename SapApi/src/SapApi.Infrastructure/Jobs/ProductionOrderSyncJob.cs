using Hangfire;
using Hangfire.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using Serilog;

namespace SapApi.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that syncs production orders for one company DB, renewing the SAP session between
/// batches until complete.
/// Prefers the requesting user's cached SAP session (Redis); falls back to SapCredentials / env password.
/// </summary>
public class ProductionOrderSyncJob(
    IHttpContextAccessor httpContextAccessor,
    ISapLoginService sapLogin,
    ProductionOrderLocalStore localStore,
    IOptions<SapCredentials> sapCredentials,
    IOptions<HangfireOptions> hangfireOptions)
{
    public const string JobName = "production-order-full-sync";

    /// <summary>
    /// One full sync at a time per companyDb argument (Hangfire lock key includes args).
    /// Concurrent enqueue is also guarded via <see cref="ProductionOrderLocalStore.TryBeginFullSyncJobAsync"/>.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        string companyDb,
        int requestingUserId,
        PerformContext? performContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyDb))
            throw new InvalidOperationException("companyDb is required for production order full sync.");

        var resolvedDb = MasterDataCacheRefreshJob.ResolveCompanyDb(companyDb);
        var sessionUserId = requestingUserId > 0
            ? requestingUserId
            : hangfireOptions.Value.ServiceUserId;
        var hangfireJobId = performContext?.BackgroundJob?.Id;

        var previous = httpContextAccessor.HttpContext;
        // Use the triggering user's id so SapLoginAsync can find their cached B1SESSION / renewal creds.
        httpContextAccessor.HttpContext =
            MasterDataCacheRefreshJob.CreateServiceHttpContext(sessionUserId, resolvedDb);

        try
        {
            if (!string.IsNullOrWhiteSpace(hangfireJobId))
                await localStore.SetFullSyncJobIdAsync(hangfireJobId, cancellationToken);

            await EnsureSapSessionAsync(sessionUserId, companyDb, resolvedDb, cancellationToken);
            await RunSyncBatchesAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            Log.Warning(ex, "Production order full sync cancelled for {CompanyDb}", companyDb);
            await TryMarkFailedAsync("Full sync cancelled before completion.", companyDb);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Production order full sync failed for {CompanyDb}", companyDb);
            await TryMarkFailedAsync($"Full sync failed: {ex.Message}", companyDb);
            throw;
        }
        finally
        {
            httpContextAccessor.HttpContext = previous;
        }
    }

    private async Task TryMarkFailedAsync(string message, string companyDb)
    {
        try
        {
            await localStore.MarkFullSyncFailedAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist Failed status for {CompanyDb}", companyDb);
        }
    }

    private async Task RunSyncBatchesAsync(CancellationToken cancellationToken)
    {
        var batch = 0;
        var totalAdded = 0;
        var totalUpdated = 0;
        int? lastAbsoluteEntry = null;

        // Phase 1: restore integer holes between consecutive local entries.
        await RunPhaseAsync(
            "gap-fill",
            cursor => localStore.SyncMissingGapsFromSapAsync(cursor, cancellationToken),
            cancellationToken);

        // Phase 2: import entries greater than the local max.
        await RunPhaseAsync(
            "new-sync",
            cursor => localStore.SyncNewFromSapAsync(cursor, cancellationToken),
            cancellationToken);

        // Phase 3: re-read orders that are still open locally. SAP exposes no last-changed field on
        // ProductionOrders, so a status move (Planned to Released, Released to Closed) is only
        // visible by re-reading the document.
        await RunPhaseAsync(
            "open-refresh",
            cursor => localStore.SyncOpenOrdersFromSapAsync(cursor, cancellationToken),
            cancellationToken);

        await localStore.MarkFullSyncSucceededAsync(totalAdded, totalUpdated, lastAbsoluteEntry, cancellationToken);
        Log.Information(
            "Production order sync completed for company: added={Added}, updated={Updated}, batches={Batches}",
            totalAdded,
            totalUpdated,
            batch);

        async Task RunPhaseAsync(
            string phase,
            Func<int?, Task<ProductionOrderSyncResult>> runBatch,
            CancellationToken token)
        {
            int? cursor = null;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                batch++;

                // Re-check expiry before each batch; SapLoginAsync renews when the session has expired.
                await sapLogin.SapLoginAsync(token);

                var result = await runBatch(cursor);
                totalAdded += result.AddedCount;
                totalUpdated += result.UpdatedCount;
                lastAbsoluteEntry = result.LastAbsoluteEntry ?? lastAbsoluteEntry;

                await localStore.UpdateFullSyncProgressAsync(result, totalAdded, totalUpdated, batch, token);

                Log.Information(
                    "Production order {Phase} batch {Batch} for {CompanyDb}: added={Added}, updated={Updated}, hasMore={HasMore}, lastEntry={LastEntry}",
                    phase,
                    batch,
                    result.CompanyDb,
                    result.AddedCount,
                    result.UpdatedCount,
                    result.HasMore,
                    result.LastAbsoluteEntry);

                if (!result.HasMore)
                    return;

                if (result.LastAbsoluteEntry is null)
                {
                    Log.Warning(
                        "Production order {Phase} reported HasMore with no cursor for {CompanyDb}; stopping.",
                        phase,
                        result.CompanyDb);
                    return;
                }

                cursor = result.LastAbsoluteEntry;
            }
        }
    }

    /// <summary>
    /// 1) Reuse / renew the requesting user's cached SAP session.
    /// 2) Else login with SapCredentials / SAP_PASSWORD for the company.
    /// </summary>
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
                "Production order sync using cached SAP session for user {UserId} on {CompanyDb}",
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
