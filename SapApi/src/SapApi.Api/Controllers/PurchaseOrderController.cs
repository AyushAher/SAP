using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrderController(SapPurchaseOrderService service) : ControllerBase
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

    /// <summary>Incremental: import POs from SAP with DocEntry greater than the local max.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncNew(CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncNewFromSapAsync(cancellationToken)));

    /// <summary>Full re-import of all purchase orders from SAP.</summary>
    [HttpPost("sync/full")]
    public async Task<IActionResult> SyncFull(CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncAllFromSapAsync(cancellationToken)));

    /// <summary>Refresh a single PO from SAP into the local table.</summary>
    [HttpPost("{docEntry:int}/sync")]
    public async Task<IActionResult> SyncOne(int docEntry, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.SyncOneFromSapAsync(docEntry, cancellationToken)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] SapQueries? query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await service.GetPurchaseOrders(id, query, cancellationToken)));

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
