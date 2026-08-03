using Hangfire;
using Hangfire.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using Serilog;

namespace SapApi.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that fully syncs purchase orders for one company DB, renewing the SAP session
/// between batches until complete.
/// Prefers the requesting user's cached SAP session (Redis); falls back to SapCredentials / env password.
/// </summary>
public class PurchaseOrderSyncJob(
    IHttpContextAccessor httpContextAccessor,
    ISapLoginService sapLogin,
    PurchaseOrderLocalStore localStore,
    IOptions<SapCredentials> sapCredentials,
    IOptions<HangfireOptions> hangfireOptions)
{
    public const string JobName = "purchase-order-full-sync";

    /// <summary>
    /// One full sync at a time per companyDb argument (Hangfire lock key includes args).
    /// Concurrent enqueue is also guarded via <see cref="PurchaseOrderLocalStore.TryBeginFullSyncJobAsync"/>.
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
            throw new InvalidOperationException("companyDb is required for purchase order full sync.");

        var resolvedDb = MasterDataCacheRefreshJob.ResolveCompanyDb(companyDb);
        var sessionUserId = requestingUserId > 0
            ? requestingUserId
            : hangfireOptions.Value.ServiceUserId;
        var hangfireJobId = performContext?.BackgroundJob?.Id;

        var previous = httpContextAccessor.HttpContext;
        // Use the triggering user's id so SapLoginAsync can find their Redis-cached B1SESSION / renewal creds.
        httpContextAccessor.HttpContext =
            MasterDataCacheRefreshJob.CreateServiceHttpContext(sessionUserId, resolvedDb);

        try
        {
            if (!string.IsNullOrWhiteSpace(hangfireJobId))
                await localStore.SetFullSyncJobIdAsync(hangfireJobId, cancellationToken);

            await EnsureSapSessionAsync(sessionUserId, companyDb, resolvedDb, cancellationToken);
            await RunFullSyncBatchesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Purchase order full sync failed for {CompanyDb}", companyDb);
            try
            {
                await localStore.MarkFullSyncFailedAsync(
                    $"Full sync failed: {ex.Message}",
                    CancellationToken.None);
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
        // Start after the highest DocEntry already stored locally (same as incremental "sync new").
        int? cursor = null;
        var batch = 0;
        var totalAdded = 0;
        var totalUpdated = 0;
        int? lastDocEntry = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch++;

            // Re-check expiry before each batch; SapLoginAsync renews when cached credentials exist.
            await sapLogin.SapLoginAsync(cancellationToken);

            // SyncNew: first batch uses max local DocEntry when cursor is null; later batches resume.
            var result = await localStore.SyncNewFromSapAsync(cursor, cancellationToken);
            totalAdded += result.AddedCount;
            totalUpdated += result.UpdatedCount;
            lastDocEntry = result.LastDocEntry ?? lastDocEntry;

            await localStore.UpdateFullSyncProgressAsync(
                result,
                totalAdded,
                totalUpdated,
                batch,
                cancellationToken);

            Log.Information(
                "PO sync batch {Batch} for {CompanyDb}: added={Added}, updated={Updated}, hasMore={HasMore}, lastDocEntry={LastDocEntry}",
                batch,
                result.CompanyDb,
                result.AddedCount,
                result.UpdatedCount,
                result.HasMore,
                result.LastDocEntry);

            if (!result.HasMore)
                break;

            if (result.LastDocEntry is null)
            {
                Log.Warning(
                    "PO sync reported HasMore but no LastDocEntry for {CompanyDb}; stopping.",
                    result.CompanyDb);
                break;
            }

            cursor = result.LastDocEntry;
        }

        await localStore.MarkFullSyncSucceededAsync(totalAdded, totalUpdated, lastDocEntry, cancellationToken);
        Log.Information(
            "PO sync completed for company: added={Added}, updated={Updated}, batches={Batches}",
            totalAdded,
            totalUpdated,
            batch);
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
                "PO full sync using cached SAP session for user {UserId} on {CompanyDb}",
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
