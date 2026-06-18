using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/business-partner")]
[Authorize]
public class BusinessPartnerController(
    BusinessPartnerService service,
    SapMasterDataService masterDataService,
    ISapLoginService sapLogin) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> ListVendors([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchVendorsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("customers/list")]
    public async Task<IActionResult> ListCustomers([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchCustomersAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("{cardCode}")]
    public async Task<IActionResult> GetByCardCode(string cardCode, CancellationToken cancellationToken)
    {
        var partner = await masterDataService.GetBusinessPartnerByCardCodeAsync(cardCode, cancellationToken);
        return partner is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Business partner not found"))
            : Ok(ApiResponse<object>.Ok(partner));
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SapBusinessPartnerRequest request, CancellationToken cancellationToken)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(await service.SaveBusinessPartners(request)));
    }
}
