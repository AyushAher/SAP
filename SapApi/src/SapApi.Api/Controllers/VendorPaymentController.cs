using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/vendor-payments")]
[Authorize]
public class VendorPaymentController(SapVendorPaymentService service) : ControllerBase
{
    [HttpGet("ap-invoices")]
    public async Task<IActionResult> GetApInvoices([FromQuery] string cardCode) =>
        Ok(ApiResponse<object>.Ok(await service.GetApInvoices(cardCode)));

    [HttpGet("grpo")]
    public async Task<IActionResult> GetGrpo([FromQuery] string cardCode) =>
        Ok(ApiResponse<object>.Ok(await service.GetGrpo(cardCode)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapVendorPaymentRequests request, [FromQuery] int? policyRequestId) =>
        Ok(ApiResponse<object>.Ok(await service.CreateVendorPayments(request, policyRequestId)));

    [HttpPost("{docEntry}/cancel")]
    public async Task<IActionResult> Cancel(string docEntry) =>
        Ok(ApiResponse<object>.Ok(await service.CancelVendorPayment(docEntry)));
}
