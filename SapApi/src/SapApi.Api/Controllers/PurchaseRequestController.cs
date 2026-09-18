using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/purchase-requests")]
[Authorize]
public class PurchaseRequestController(
    SapPurchaseRequestService service,
    PurchaseRequestLocalStore localStore,
    ICurrentCompanyDbAccessor companyDbAccessor,
    IHttpContextAccessor httpContextAccessor,
    IOptions<HangfireOptions> hangfireOptions,
    IServiceProvider services,
    IPdfService pdfService,
    PurchaseRequestPdfBuilder pdfBuilder) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await service.GetAllPurchaseRequestsPaginated(PaginationRequest.Normalize(request), cancellationToken));

    /// <summary>
    /// Purchase Request report PDF: every line of every request matching the filter dialog
    /// (username, date range, required-by range, project range, period, branch), read live from
    /// SAP — the report must reflect SAP's current state, not the local sync mirror's.
    /// </summary>
    [HttpPost("report/pdf")]
    public async Task<IActionResult> ReportPdf([FromBody] List<FilterModel>? filters, CancellationToken cancellationToken)
    {
        var rows = await service.GetReportRowsFromSapAsync(filters ?? [], cancellationToken);
        var placeholders = PurchaseRequestReportPdfBuilder.BuildPlaceholders(rows, filters ?? []);
        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "purchase-request-report-template.html", placeholders, cancellationToken);
        return File(pdfBytes, "application/pdf", $"PurchaseRequestReport({DateTime.UtcNow:yyyyMMddHHmmss}).pdf");
    }

    [HttpGet("{docEntry:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int docEntry, CancellationToken cancellationToken)
    {
        var pr = await service.GetPurchaseRequests(docEntry.ToString(), null, cancellationToken);
        if (pr is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase request not found"));

        var placeholders = await pdfBuilder.BuildPlaceholdersAsync(pr, cancellationToken);
        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "purchase-request-template.html", placeholders, cancellationToken);

        var fileName = $"PurchaseRequisition({pr.DocNum ?? docEntry}).pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpGet("sync-status")]
    public async Task<IActionResult> SyncStatus(CancellationToken cancellationToken)
    {
        var status = await service.GetSyncStateAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Ok(status));
    }

    /// <summary>
    /// Enqueues a Hangfire job that fully syncs all purchase requests for the current company.
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
                    "Purchase request background sync is unavailable (Hangfire is disabled)."));
        }

        var existing = await localStore.GetSyncStateAsync(cancellationToken);
        if (existing is not null
            && string.Equals(existing.Status, PurchaseRequestSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
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
                status = raced?.Status ?? PurchaseRequestSyncState.StatusRunning,
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
            jobId = backgroundJobs.Enqueue<PurchaseRequestSyncJob>(
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
            status = PurchaseRequestSyncState.StatusRunning,
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

    /// <summary>Full re-import of all purchase requests from SAP. Resumable via afterDocEntry.</summary>
    [HttpPost("sync/full")]
    public async Task<IActionResult> SyncFull([FromQuery] int? afterDocEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncAllFromSapAsync(afterDocEntry, cancellationToken)));

    /// <summary>Refresh a single PO from SAP into the local table.</summary>
    [HttpPost("{docEntry:int}/sync")]
    public async Task<IActionResult> SyncOne(int docEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncOneFromSapAsync(docEntry, cancellationToken)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] SapQueries? query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.GetPurchaseRequests(id, query, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapPurchaseRequestsResponse data, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreatePurchaseRequest(data, policyRequestId)));

    [HttpPut("{docEntry:int}")]
    public async Task<IActionResult> Update(int docEntry, [FromBody] SapPurchaseRequestsResponse data, [FromQuery] int? policyRequestId)
    {
        data.DocEntry = docEntry;
        return Ok(ApiResponse<object>.Ok(await service.UpdatePurchaseRequest(data, policyRequestId)));
    }

    /// <summary>
    /// Cancels the purchase request in SAP (<c>POST PurchaseRequests(id)/Cancel</c>).
    /// HTTP DELETE is not allowed on this company DB.
    /// </summary>
    [HttpPost("{docEntry:int}/cancel")]
    public async Task<IActionResult> Cancel(int docEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.CancelPurchaseRequest(docEntry, cancellationToken)));
}
