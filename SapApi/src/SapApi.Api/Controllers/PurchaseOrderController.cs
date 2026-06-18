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
    public async Task<IActionResult> List([FromBody] PaginationRequest? request) =>
        Ok(await service.GetAllPurchaseOrdersPaginated(PaginationRequest.Normalize(request)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] SapQueries? query) =>
        Ok(ApiResponse<object>.Ok(await service.GetPurchaseOrders(id, query)));

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
