using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/inventory-gen-exits")]
[Authorize]
public class InventoryGenExitsController(SapInventoryGenExitsService service) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request) =>
        Ok(await service.GetAllPaginated(PaginationRequest.Normalize(request)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapInventoryGenExitRequestOrderRequest request, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreateAsync(request, policyRequestId)));
}
