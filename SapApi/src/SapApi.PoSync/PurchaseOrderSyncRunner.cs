using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using Serilog;

namespace SapApi.PoSync;

/// <summary>
/// Syncs purchase orders from SAP Service Layer into PostgreSQL using the same
/// <see cref="SapPurchaseOrderService"/> / session stack as the API.
/// </summary>
public sealed class PurchaseOrderSyncRunner(
    IHttpContextAccessor httpContextAccessor,
    ISapLoginService sapLogin,
    SapPurchaseOrderService purchaseOrderService,
    AppDbContext db,
    IOptions<SapCredentials> sapCredentials,
    IOptions<PurchaseOrderSyncOptions> syncOptions)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var options = syncOptions.Value;
        var mode = (options.Mode ?? "new").Trim().ToLowerInvariant();
        if (mode is not ("new" or "full" or "one"))
        {
            Log.Error("Invalid PurchaseOrderSync:Mode '{Mode}'. Use new, full, or one.", options.Mode);
            return 1;
        }

        if (mode == "one" && options.DocEntry is null or <= 0)
        {
            Log.Error("PurchaseOrderSync:Mode=one requires PurchaseOrderSync:DocEntry > 0.");
            return 1;
        }

        var accounts = ResolveAccounts(options);
        if (accounts.Count == 0)
        {
            Log.Error(
                "No SapCredentials:Accounts to sync. Configure Username, Password, and CompanyDb in appsettings "
                + "(or filter PurchaseOrderSync:CompanyDb to a configured account).");
            return 1;
        }

        if (options.MigrateDatabase)
        {
            Log.Information("Applying EF Core migrations…");
            await db.Database.MigrateAsync(cancellationToken);
        }

        var failures = new List<string>();

        foreach (var account in accounts)
        {
            try
            {
                await SyncAccountAsync(account, options, mode, cancellationToken);
            }
            catch (Exception ex)
            {
                var company = account.CompanyDb ?? "(missing CompanyDb)";
                Log.Error(ex, "Purchase order sync failed for {CompanyDb}", company);
                failures.Add($"{company}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            Log.Error(
                "Sync finished with {FailureCount}/{TotalCount} company failure(s): {Failures}",
                failures.Count,
                accounts.Count,
                string.Join("; ", failures));
            return 1;
        }

        Log.Information("Purchase order sync completed for {Count} company DB(s).", accounts.Count);
        return 0;
    }

    private List<SapCompanyCredential> ResolveAccounts(PurchaseOrderSyncOptions options)
    {
        var accounts = sapCredentials.Value.Accounts ?? [];
        if (string.IsNullOrWhiteSpace(options.CompanyDb))
            return accounts;

        return accounts
            .Where(a => string.Equals(a.CompanyDb, options.CompanyDb, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task SyncAccountAsync(
        SapCompanyCredential account,
        PurchaseOrderSyncOptions options,
        string mode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.Username))
            throw new InvalidOperationException("SapCredentials account Username is required.");

        if (string.IsNullOrWhiteSpace(account.CompanyDb))
            throw new InvalidOperationException("SapCredentials account CompanyDb is required.");

        var password = MasterDataCacheRefreshJob.ResolvePassword(account);
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                $"SAP password is required for CompanyDb '{account.CompanyDb}'. "
                + "Set SapCredentials:Accounts:N:Password in appsettings, or "
                + "SapCredentials__Accounts__N__Password / SAP_PASSWORD / SAP_PASSWORD_{CompanyDb}.");

        var companyDb = MasterDataCacheRefreshJob.ResolveCompanyDb(account.CompanyDb);
        var serviceUserId = options.ServiceUserId;

        var previous = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = MasterDataCacheRefreshJob.CreateServiceHttpContext(serviceUserId, companyDb);
        try
        {
            await EnsureSapSessionAsync(serviceUserId, account.Username!, password!, companyDb, cancellationToken);

            if (mode == "one")
            {
                var one = await purchaseOrderService.SyncOneFromSapAsync(options.DocEntry!.Value, cancellationToken);
                Log.Information(
                    "Synced DocEntry {DocEntry} for {CompanyDb}: {Message}",
                    options.DocEntry,
                    companyDb,
                    one.Message);
                return;
            }

            await SyncBatchesAsync(companyDb, mode, options, cancellationToken);
        }
        finally
        {
            httpContextAccessor.HttpContext = previous;
        }
    }

    private async Task SyncBatchesAsync(
        SapCompanyDatabase companyDb,
        string mode,
        PurchaseOrderSyncOptions options,
        CancellationToken cancellationToken)
    {
        int? cursor = options.AfterDocEntry;
        var batch = 0;
        var totalAdded = 0;
        var totalUpdated = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch++;

            // Re-check expiry before each batch; SapLoginAsync renews when ExpiresAtUtc has passed
            // (or when stored credentials are present). HttpRequestHandler also renews on SAP error 301.
            await sapLogin.SapLoginAsync(cancellationToken);

            PurchaseOrderSyncResult result = mode == "full"
                ? await purchaseOrderService.SyncAllFromSapAsync(cursor, cancellationToken)
                : await purchaseOrderService.SyncNewFromSapAsync(cursor, cancellationToken);

            totalAdded += result.AddedCount;
            totalUpdated += result.UpdatedCount;

            Log.Information(
                "Batch {Batch} for {CompanyDb} ({Mode}): added={Added}, updated={Updated}, hasMore={HasMore}, lastDocEntry={LastDocEntry}. {Message}",
                batch,
                companyDb,
                mode,
                result.AddedCount,
                result.UpdatedCount,
                result.HasMore,
                result.LastDocEntry,
                result.Message);

            if (!result.HasMore || !options.ContinueUntilComplete)
                break;

            if (result.LastDocEntry is null)
            {
                Log.Warning(
                    "Sync reported HasMore but no LastDocEntry for {CompanyDb}; stopping to avoid an infinite loop.",
                    companyDb);
                break;
            }

            cursor = result.LastDocEntry;
        }

        Log.Information(
            "Finished {Mode} sync for {CompanyDb}: batches={Batches}, added={Added}, updated={Updated}",
            mode,
            companyDb,
            batch,
            totalAdded,
            totalUpdated);
    }

    /// <summary>
    /// Prefer a still-valid cached session; otherwise establish one with configured credentials
    /// (same pattern as <see cref="MasterDataCacheRefreshJob"/>).
    /// </summary>
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
            Log.Information("Reusing valid SAP session for {CompanyDb} (user {SapUser}).", companyDb, userName);
        }
        catch (ApiErrorException)
        {
            Log.Information("No valid SAP session for {CompanyDb}; logging in as {SapUser}.", companyDb, userName);
            await sapLogin.LoginWithUserCredentialsAsync(serviceUserId, userName, password, companyDb, cancellationToken);
        }
    }
}
