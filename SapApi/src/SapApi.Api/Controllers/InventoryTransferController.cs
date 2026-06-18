using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/inventory-transfers")]
[Authorize]
public class InventoryTransferController(InventoryItemsTransferService service) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request) =>
        Ok(await service.GetAllInventoryTransferRequestsPaginated(PaginationRequest.Normalize(request)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id) =>
        Ok(ApiResponse<object>.Ok(await service.GetInventoryTransferRequests(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapInventoryTransferRequestsRequest request, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreateRequest(request, policyRequestId)));

    [HttpPut("{docEntry}")]
    public async Task<IActionResult> Update(string docEntry, [FromBody] SapInventoryTransferRequestsRequest request, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.UpdateRequest(request, docEntry, policyRequestId)));

    [HttpPost("{docEntry}/close")]
    public async Task<IActionResult> Close(string docEntry)
    {
        await service.CloseTransferRequest(docEntry);
        return Ok(ApiResponse<object>.Ok(null, "Closed"));
    }

    [HttpPost("{docEntry}/cancel")]
    public async Task<IActionResult> Cancel(string docEntry)
    {
        await service.CancelTransferRequest(docEntry);
        return Ok(ApiResponse<object>.Ok(null, "Cancelled"));
    }
}
