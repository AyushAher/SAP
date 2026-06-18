using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/issue-for-production")]
[Authorize]
public class IssueForProductionController(IssueForProductionService service) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var (items, totalCount) = await service.ListAsync(normalized, cancellationToken);
        return Ok(PaginationResponseFactory.Create(normalized, items, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(id, cancellationToken);
        return item == null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"))
            : Ok(ApiResponse<object>.Ok(item));
    }

    [HttpGet("{id:int}/order-lines")]
    public async Task<IActionResult> GetOrderLines(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(id, cancellationToken);
        if (item is null) return NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
        var orderLines = ProductionRequestMapper.ParseOrderLines(item.RequestBody);
        return Ok(ApiResponse<object>.Ok(orderLines));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapInventoryGenExitRequestOrderLines orderLines, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await service.SaveAsync(orderLines, null, cancellationToken);
            return Ok(ApiResponse<object>.Ok(entity));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("VAL-01", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SapInventoryGenExitRequestOrderLines orderLines, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await service.SaveAsync(orderLines, id, cancellationToken);
            return Ok(ApiResponse<object>.Ok(entity));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("VAL-01", ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted
            ? Ok(ApiResponse<object>.Ok(null, "Deleted"))
            : NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
    }
}
