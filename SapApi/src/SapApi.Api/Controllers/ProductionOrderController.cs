using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/production-orders")]
[Authorize]
public class ProductionOrderController(SapProductionOrdersService service) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request) =>
        Ok(await service.GetAllProductionOrdersPaginated(PaginationRequest.Normalize(request)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id) =>
        Ok(ApiResponse<object>.Ok(await service.GetProductionOrders(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapProductionOrdersResponse data, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreateProductionOrderAsync(data, policyRequestId)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SapProductionOrdersResponse data, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.UpdateProductionOrderAsync(data, policyRequestId)));

    [HttpGet("{id}/lines")]
    public async Task<IActionResult> GetLines(string id) =>
        Ok(ApiResponse<object>.Ok(await service.GetProductionOrderLines(id)));

    [HttpPost("select/{absoluteEntry}")]
    public async Task<IActionResult> Select(string absoluteEntry, [FromServices] ProductionOrderSelectionService selectionService, CancellationToken cancellationToken)
    {
        var result = await selectionService.BuildSelectionAsync(absoluteEntry, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Production order not found"))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("{absoluteEntry}/add-line")]
    public async Task<IActionResult> AddLine(string absoluteEntry, [FromBody] SapProductionOrderLines line, [FromServices] ProductionOrderSelectionService selectionService, CancellationToken cancellationToken)
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
