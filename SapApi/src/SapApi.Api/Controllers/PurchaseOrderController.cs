using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrderController(
    SapPurchaseOrderService service,
    PurchaseOrderLocalStore localStore,
    PurchaseOrderPdfBuilder pdfBuilder,
    IPdfService pdfService,
    ICurrentCompanyDbAccessor companyDbAccessor,
    IHttpContextAccessor httpContextAccessor,
    IOptions<HangfireOptions> hangfireOptions,
    IServiceProvider services) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await service.GetAllPurchaseOrdersPaginated(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("sync-status")]
    public async Task<IActionResult> SyncStatus(CancellationToken cancellationToken)
    {
        var status = await service.GetSyncStateAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Ok(status));
    }

    /// <summary>
    /// Enqueues a Hangfire job that fully syncs all purchase orders for the current company.
    /// Returns the existing job if one is already Running.
    /// </summary>
    [HttpPost("sync/jobs/full")]
    public async Task<IActionResult> EnqueueFullSyncJob(CancellationToken cancellationToken)
    {
        var hangfire = hangfireOptions.Value;
        var backgroundJobs = services.GetService<IBackgroundJobClient>();
        if (!hangfire.Enabled || backgroundJobs is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    BaseErrorCodes.ValidationFailed,
                    "Purchase order background sync is unavailable (Hangfire is disabled)."));
        }

        var existing = await localStore.GetSyncStateAsync(cancellationToken);
        if (existing is not null
            && string.Equals(existing.Status, PurchaseOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                jobId = existing.HangfireJobId,
                status = existing.Status,
                message = existing.Message,
                alreadyRunning = true,
            }));
        }

        var started = await localStore.TryBeginFullSyncJobAsync(hangfireJobId: null, cancellationToken);
        if (!started)
        {
            var raced = await localStore.GetSyncStateAsync(cancellationToken);
            return Ok(ApiResponse<object>.Ok(new
            {
                jobId = raced?.HangfireJobId,
                status = raced?.Status ?? PurchaseOrderSyncState.StatusRunning,
                message = raced?.Message ?? "Full sync already running.",
                alreadyRunning = true,
            }));
        }

        var companyDb = companyDbAccessor.GetCompanyDbName();
        var requestingUserId = httpContextAccessor.GetUserIdAsync()
            ?? hangfire.ServiceUserId;
        string jobId;
        try
        {
            jobId = backgroundJobs.Enqueue<PurchaseOrderSyncJob>(
                job => job.ExecuteAsync(companyDb, requestingUserId, null!, CancellationToken.None));
        }
        catch (Exception ex)
        {
            await localStore.MarkFullSyncFailedAsync(
                $"Failed to enqueue full sync job: {ex.Message}",
                cancellationToken);
            throw;
        }

        await localStore.SetFullSyncJobIdAsync(jobId, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            jobId,
            status = PurchaseOrderSyncState.StatusRunning,
            message = "Full sync job queued.",
            alreadyRunning = false,
        }));
    }

    /// <summary>
    /// Incremental: import POs from SAP with DocEntry greater than the local max. Work is capped per
    /// call so the request cannot be killed by a reverse-proxy read timeout; when the response has
    /// hasMore=true, call again with afterDocEntry=lastDocEntry to continue.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncNew([FromQuery] int? afterDocEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncNewFromSapAsync(afterDocEntry, cancellationToken)));

    /// <summary>Full re-import of all purchase orders from SAP. Resumable via afterDocEntry.</summary>
    [HttpPost("sync/full")]
    public async Task<IActionResult> SyncFull([FromQuery] int? afterDocEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncAllFromSapAsync(afterDocEntry, cancellationToken)));

    /// <summary>Refresh a single PO from SAP into the local table.</summary>
    [HttpPost("{docEntry:int}/sync")]
    public async Task<IActionResult> SyncOne(int docEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncOneFromSapAsync(docEntry, cancellationToken)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] SapQueries? query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.GetPurchaseOrders(id, query, cancellationToken)));

    [HttpGet("{docEntry:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int docEntry, CancellationToken cancellationToken)
    {
        var order = await service.GetPurchaseOrders(docEntry.ToString(), null, cancellationToken);
        if (order is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"));

        var placeholders = await pdfBuilder.BuildPlaceholdersAsync(
            order,
            User.Identity?.Name,
            cancellationToken);

        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "purchase-order-template.html", placeholders, cancellationToken);

        var fileName = $"PurchaseOrder({order.DocNum ?? docEntry}).pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapPurchaseOrdersResponse data, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreatePurchaseOrder(data, policyRequestId)));

    [HttpPut("{docEntry:int}")]
    public async Task<IActionResult> Update(int docEntry, [FromBody] SapPurchaseOrdersResponse data, [FromQuery] int? policyRequestId)
    {
        data.DocEntry = docEntry;
        return Ok(ApiResponse<object>.Ok(await service.UpdatePurchaseOrder(data, policyRequestId)));
    }
}
