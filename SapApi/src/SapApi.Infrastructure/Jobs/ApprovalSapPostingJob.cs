using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Requests;
using Serilog;

namespace SapApi.Infrastructure.Jobs;

/// <summary>
/// Posts a finalized approval to SAP in the background — any document type — so the approve API
/// can return immediately instead of waiting on Service Layer.
/// </summary>
public class ApprovalSapPostingJob(IHttpContextAccessor httpContextAccessor, IServiceScopeFactory scopeFactory)
{
    [Hangfire.AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(
        string companyDb,
        int requestId,
        int requestingUserId,
        string? comment,
        string? utrNo,
        DateTime? utrDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyDb))
            throw new InvalidOperationException("companyDb is required for approval SAP posting.");

        var resolvedDb = MasterDataCacheRefreshJob.ResolveCompanyDb(companyDb);
        var previous = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = MasterDataCacheRefreshJob.CreateServiceHttpContext(
            requestingUserId > 0 ? requestingUserId : 0,
            resolvedDb);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<ApprovalExecutionService>();

            var request = await db.ApprovalRequests
                .Include(x => x.UserApprovals)
                .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
                ?? throw new InvalidOperationException($"Approval request {requestId} was not found.");

            var actionData = new ApprovalActionData
            {
                Action = "Approve",
                Comment = comment ?? "Approved",
                UtrNo = utrNo,
                UtrDate = utrDate,
            };
            var sapResponse = await executionService.ExecuteAsync(request, actionData, cancellationToken);
            await executionService.FinalizeApprovalAsync(request, actionData, sapResponse, cancellationToken);

            if (sapResponse is null)
            {
                Log.Warning(
                    "Background approval {ApprovalRequestId} recorded but SAP posting did not run",
                    requestId);
                return;
            }

            if (!string.IsNullOrEmpty(sapResponse.Error?.Message?.Value))
            {
                Log.Warning(
                    "Background approval {ApprovalRequestId} SAP posting failed: {Message}",
                    requestId,
                    sapResponse.Error.Message.Value);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Background approval SAP posting failed for request {ApprovalRequestId}", requestId);
            throw;
        }
        finally
        {
            httpContextAccessor.HttpContext = previous;
        }
    }
}
