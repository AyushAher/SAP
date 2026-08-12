using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/production-orders")]
[Authorize]
public class ProductionOrderController(
    SapProductionOrdersService service,
    ProductionOrderLocalStore localStore,
    ICurrentCompanyDbAccessor companyDbAccessor,
    IHttpContextAccessor httpContextAccessor,
    IOptions<HangfireOptions> hangfireOptions,
    IServiceProvider services) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await service.GetAllProductionOrdersPaginated(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var order = await service.GetProductionOrders(id, cancellationToken: cancellationToken);
        return order is null
            ? NotFound(ApiResponse<object>.Fail(BaseErrorCodes.NullValue, "Production order not found"))
            : Ok(ApiResponse<object>.Ok(order));
    }

    [HttpGet("{id}/lines")]
    public async Task<IActionResult> GetLines(string id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(new { value = await service.GetProductionOrderLines(id, cancellationToken) }));

    [HttpGet("sync-status")]
    public async Task<IActionResult> SyncStatus(CancellationToken cancellationToken) =>
        Ok(ApiResponse<object?>.Ok(await service.GetSyncStateAsync(cancellationToken)));

    /// <summary>Audit trail of production order syncs for the current company.</summary>
    [HttpPost("sync-history")]
    public async Task<IActionResult> SyncHistory(
        [FromBody] PaginationRequest? request,
        CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var (items, totalCount) = await localStore.ListAuditAsync(normalized, cancellationToken);
        var rows = items.Select(x => new
        {
            x.Mode,
            x.AbsoluteEntry,
            x.UserName,
            x.CorrelationId,
            x.AddedCount,
            x.UpdatedCount,
            x.Succeeded,
            x.Message,
            x.DurationMs,
            x.CreatedOn,
        }).ToList();
        return Ok(PaginationResponseFactory.Create(normalized, rows, totalCount));
    }

    /// <summary>
    /// Enqueues a Hangfire job that syncs every production order for the current company: fill
    /// entry gaps, import newer orders, then refresh orders that are still open.
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
                    "Production order background sync is unavailable (Hangfire is disabled)."));
        }

        var existing = await localStore.GetSyncStateAsync(cancellationToken);
        if (existing is not null
            && string.Equals(existing.Status, ProductionOrderSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
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
                status = raced?.Status ?? ProductionOrderSyncState.StatusRunning,
                message = raced?.Message ?? "Full sync already running.",
                alreadyRunning = true,
            }));
        }

        var companyDb = companyDbAccessor.GetCompanyDbName();
        var requestingUserId = httpContextAccessor.GetUserIdAsync() ?? hangfire.ServiceUserId;
        string jobId;
        try
        {
            jobId = backgroundJobs.Enqueue<ProductionOrderSyncJob>(
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
            status = ProductionOrderSyncState.StatusRunning,
            message = "Full sync job queued.",
            alreadyRunning = false,
        }));
    }

    /// <summary>
    /// Incremental: import production orders from SAP with AbsoluteEntry greater than the local
    /// max. Work is capped per call so the request cannot be killed by a reverse-proxy read
    /// timeout; when the response has hasMore=true, call again with
    /// afterAbsoluteEntry=lastAbsoluteEntry to continue.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncNew([FromQuery] int? afterAbsoluteEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncNewFromSapAsync(afterAbsoluteEntry, cancellationToken)));

    /// <summary>Full re-import of all production orders from SAP. Resumable via afterAbsoluteEntry.</summary>
    [HttpPost("sync/full")]
    public async Task<IActionResult> SyncFull([FromQuery] int? afterAbsoluteEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncAllFromSapAsync(afterAbsoluteEntry, cancellationToken)));

    /// <summary>Refresh a single production order from SAP into the local tables.</summary>
    [HttpPost("{absoluteEntry:int}/sync")]
    public async Task<IActionResult> SyncOne(int absoluteEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncOneFromSapAsync(absoluteEntry, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SapProductionOrdersResponse data,
        [FromQuery] int? policyRequestId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.CreateProductionOrderAsync(data, policyRequestId, cancellationToken)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SapProductionOrdersResponse data,
        [FromQuery] int? policyRequestId,
        CancellationToken cancellationToken)
    {
        data.AbsoluteEntry = id;
        return Ok(ApiResponse<object>.Ok(await service.UpdateProductionOrderAsync(data, policyRequestId, cancellationToken)));
    }

    [HttpPost("select/{absoluteEntry}")]
    public async Task<IActionResult> Select(
        string absoluteEntry,
        [FromServices] ProductionOrderSelectionService selectionService,
        CancellationToken cancellationToken)
    {
        var result = await selectionService.BuildSelectionAsync(absoluteEntry, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Production order not found"))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("{absoluteEntry}/add-line")]
    public async Task<IActionResult> AddLine(
        string absoluteEntry,
        [FromBody] SapProductionOrderLines line,
        [FromServices] ProductionOrderSelectionService selectionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await selectionService.AddManualLineAsync(absoluteEntry, line, cancellationToken);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Production order not found"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("VAL-01", ex.Message));
        }
    }
}
