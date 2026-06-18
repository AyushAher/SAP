using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/grpo")]
[Authorize]
public class GrpoController(SapPurchaseOrderService purchaseOrderService) : ControllerBase
{
    [HttpGet("from-po/{docEntry:int}")]
    public async Task<IActionResult> GetFromPo(int docEntry) =>
        Ok(ApiResponse<object>.Ok(await purchaseOrderService.GetPurchaseOrders(docEntry.ToString())));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapPurchaseOrdersResponse data) =>
        Ok(ApiResponse<object>.Ok(await purchaseOrderService.CreateGrpo(data)));
}
