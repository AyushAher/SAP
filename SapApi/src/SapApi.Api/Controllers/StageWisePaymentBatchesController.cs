using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/stage-wise-payment-batches")]
[Authorize]
public class StageWisePaymentBatchesController(
    StageWisePaymentBatchService batchService,
    StageWisePaymentPageService pageService,
    StageWisePaymentPdfBuilder pdfBuilder,
    IPdfService pdfService) : ControllerBase
{
    [HttpGet("page-data/{poDocEntry:int}")]
    public async Task<IActionResult> GetPageData(int poDocEntry, CancellationToken cancellationToken)
    {
        var data = await pageService.LoadPageDataAsync(poDocEntry, cancellationToken);
        return data is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"))
            : Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("calculate-line")]
    public async Task<IActionResult> CalculateLine([FromBody] CalculateBatchLineRequest request, CancellationToken cancellationToken)
    {
        var result = await batchService.CalculateLineAsync(request, cancellationToken);
        return result is null
            ? BadRequest(ApiResponse<object>.Fail("SYS-02", "Unable to calculate line amounts"))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStageWisePaymentBatchRequest request, CancellationToken cancellationToken)
    {
        var (success, message, data) = await batchService.CreateBatchAsync(request, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(data, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpPut("{batchId:int}")]
    public async Task<IActionResult> Update(int batchId, [FromBody] CreateStageWisePaymentBatchRequest request, CancellationToken cancellationToken)
    {
        var (success, message, data) = await batchService.UpdateBatchAsync(batchId, request, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(data, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpPost("{batchId:int}/submit")]
    public async Task<IActionResult> Submit(int batchId, [FromBody] CreateStageWisePaymentBatchRequest request, CancellationToken cancellationToken)
    {
        var (success, message, data) = await batchService.SubmitBatchAsync(batchId, request, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(data, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpPost("{batchId:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int batchId, CancellationToken cancellationToken)
    {
        var (success, message, data) = await batchService.WithdrawBatchApprovalAsync(batchId, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(data, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpPut("{batchId:int}/additional-details")]
    public async Task<IActionResult> UpdateAdditionalDetails(
        int batchId,
        [FromBody] UpdateBatchAdditionalDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var (success, message, data) = await batchService.UpdateAdditionalDetailsAsync(batchId, request, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(data, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpGet("{batchId:int}")]
    public async Task<IActionResult> GetById(int batchId, CancellationToken cancellationToken)
    {
        var data = await batchService.GetBatchAsync(batchId, cancellationToken);
        return data is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Batch payment not found"))
            : Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("by-stage-wise-payment/{stageWisePaymentId:int}")]
    public async Task<IActionResult> GetByStageWisePaymentId(int stageWisePaymentId, CancellationToken cancellationToken)
    {
        var data = await batchService.GetBatchByStageWisePaymentIdAsync(stageWisePaymentId, cancellationToken);
        return data is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Batch payment not found"))
            : Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("by-approval/{approvalRequestId:int}")]
    public async Task<IActionResult> GetByApprovalRequestId(int approvalRequestId, CancellationToken cancellationToken)
    {
        var data = await batchService.GetBatchByApprovalRequestIdAsync(approvalRequestId, cancellationToken);
        return data is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Batch payment not found for this approval"))
            : Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("{batchId:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int batchId, CancellationToken cancellationToken)
    {
        var batch = await batchService.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Batch payment not found"));

        var record = await batchService.GetPrimaryPaymentRecordAsync(batchId, cancellationToken);
        if (record is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "No payment record linked to this batch"));

        var pageData = await pageService.LoadPageDataAsync(batch.PoDocEntry, cancellationToken);
        if (pageData?.PurchaseOrder is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"));

        var placeholders = await pdfBuilder.BuildPlaceholdersAsync(
            record,
            pageData,
            User.Identity?.Name,
            cancellationToken);

        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "outgoing-payment-template.html", placeholders, cancellationToken);

        var fileName = $"Batch Payment Requisition({record.ApDownPaymentInvoiceEntryNumber ?? batchId.ToString()}).pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost("{batchId:int}/cancel")]
    public async Task<IActionResult> Cancel(int batchId, CancellationToken cancellationToken)
    {
        var (success, message, operations) = await batchService.CancelBatchAsync(batchId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { success, operations }, message));
    }

    [HttpDelete("{batchId:int}")]
    public async Task<IActionResult> Delete(int batchId, CancellationToken cancellationToken)
    {
        var (success, message) = await batchService.DeleteBatchAsync(batchId, cancellationToken);
        return success
            ? Ok(ApiResponse<object>.Ok(null, message))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }
}
