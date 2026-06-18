using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/masters")]
[Authorize]
public class MastersController(SapMasterDataService masterDataService) : ControllerBase
{
    [HttpPost("items/list")]
    public async Task<IActionResult> ListItems([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchItemsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("items/{itemCode}")]
    public async Task<IActionResult> GetItem(string itemCode, CancellationToken cancellationToken)
    {
        var item = await masterDataService.GetItemByCodeAsync(itemCode, cancellationToken);
        return item is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Item not found"))
            : Ok(ApiResponse<object>.Ok(item));
    }

    [HttpPost("warehouses/list")]
    public async Task<IActionResult> ListWarehouses([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchWarehousesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("tax-codes/list")]
    public async Task<IActionResult> ListTaxCodes([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchTaxCodesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("projects/list")]
    public async Task<IActionResult> ListProjects([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchProjectsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("projects/{projectCode}")]
    public async Task<IActionResult> GetProject(string projectCode, CancellationToken cancellationToken)
    {
        var project = await masterDataService.GetProjectByCodeAsync(projectCode, cancellationToken);
        return project is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Project not found"))
            : Ok(ApiResponse<object>.Ok(project));
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] MasterLookupRequest? request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<MasterLookupResponse>.Ok(await masterDataService.LookupMasterDataAsync(request ?? new MasterLookupRequest(), cancellationToken)));

    [HttpPost("business-places/list")]
    public async Task<IActionResult> ListBusinessPlaces([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchBusinessPlacesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("sales-orders/list")]
    public async Task<IActionResult> ListSalesOrders(
        [FromBody] PaginationRequest? request,
        [FromQuery] string? customerId,
        CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchSalesOrdersAsync(PaginationRequest.Normalize(request), customerId, cancellationToken));
}
