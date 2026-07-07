using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/stage-wise-payments")]
[Authorize]
public class StageWisePaymentsController(
    AppDbContext db,
    StageWisePaymentService service,
    StageWisePaymentPageService pageService,
    StageWisePaymentPdfBuilder pdfBuilder,
    SapPurchaseOrderService purchaseOrderService,
    IPdfService pdfService,
    ICurrentCompanyDbAccessor companyDbAccessor) : ControllerBase
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();
    [HttpGet("page-data/{poDocEntry:int}")]
    public async Task<IActionResult> GetPageData(int poDocEntry, CancellationToken cancellationToken)
    {
        var data = await pageService.LoadPageDataAsync(poDocEntry, cancellationToken);
        return data is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"))
            : Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetByPo([FromQuery] int poDocEntry, CancellationToken cancellationToken)
    {
        var po = await purchaseOrderService.GetPurchaseOrderForPaymentPage(poDocEntry.ToString(), cancellationToken);
        var docNum = po?.DocNum ?? poDocEntry;
        var records = await db.StageWisePayments
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == docNum)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(records));
    }

    [HttpGet("payment-terms/{poDocEntry:int}")]
    public async Task<IActionResult> GetPaymentTerms(int poDocEntry, CancellationToken cancellationToken)
    {
        var po = await purchaseOrderService.GetPurchaseOrderForPaymentPage(poDocEntry.ToString(), cancellationToken);
        if (po == null) return NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"));
        return Ok(ApiResponse<object>.Ok(po.CreateUdfList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStageWisePaymentApiRequest request, CancellationToken cancellationToken)
    {
        var po = await purchaseOrderService.GetPurchaseOrderForPaymentOperations(request.PoDocEntry.ToString(), cancellationToken);
        if (po == null) return BadRequest(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"));

        if (StageWisePaymentCalculations.RequiresBatchPayment(po, request.SelectedPaymentTermsUdf, request.ApInvoiceDocEntry))
            return BadRequest(ApiResponse<object>.Fail("SYS-01", "AP invoice payments must be created using batch payment."));

        var totalBasic = (po.DocTotal ?? 0) - (po.VatSum ?? 0);
        var existing = await db.StageWisePayments
            .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == (request.DocNumber ?? po.DocNum))
            .ToListAsync(cancellationToken);

        var activeRecords = existing.Where(x => x.Status != StageWisePaymentStatus.Cancelled).ToList();
        var selectedApInvoice = await pageService.ResolveApInvoiceAsync(po, request.ApInvoiceDocEntry, cancellationToken);

        var payable = StageWisePaymentCalculations.ResolvePayableForCreate(
            po,
            request.SelectedPaymentTermsUdf,
            activeRecords,
            selectedApInvoice,
            request.ApInvoiceDocEntry,
            totalBasic);

        var entity = new StageWisePayment
        {
            CompanyDb = CompanyDb,
            PaymentTermsType = request.PaymentTermsType ?? request.SelectedPaymentTermsUdf.Id,
            StageDesc = request.StageDesc ?? request.SelectedPaymentTermsUdf.Desc,
            Bank = request.Bank,
            ApInvoiceDocEntry = request.ApInvoiceDocEntry,
            DocNumber = request.DocNumber ?? po.DocNum,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            Stage = MapStage(request.SelectedPaymentTermsUdf.Type)
        };

        var (success, message, _) = await service.CreateStageWisePayment(
            entity, po, request.SelectedPaymentTermsUdf, request.DownPaymentAmount,
            totalBasic, payable, request.WtCode, request.Desc, existing);

        return success
            ? Ok(ApiResponse<object>.Ok(new { message }))
            : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id, [FromQuery] int poDocEntry, CancellationToken cancellationToken)
    {
        var record = await db.StageWisePayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);
        if (record is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Record not found"));

        var pageData = await pageService.LoadPageDataAsync(poDocEntry, cancellationToken);
        if (pageData?.PurchaseOrder is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Purchase order not found"));

        var placeholders = await pdfBuilder.BuildPlaceholdersAsync(
            record,
            pageData,
            User.Identity?.Name,
            cancellationToken);

        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "outgoing-payment-template.html", placeholders, cancellationToken);

        var fileName = $"Payment Requisition({record.ApDownPaymentInvoiceEntryNumber ?? record.Id.ToString()}).pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var record = await db.StageWisePayments.FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);
        if (record == null) return NotFound(ApiResponse<object>.Fail("SYS-02", "Record not found"));
        var (success, message) = await service.DeleteStageWisePayment(record);
        return success ? Ok(ApiResponse<object>.Ok(null, message)) : BadRequest(ApiResponse<object>.Fail("SYS-01", message));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var record = await db.StageWisePayments.FirstOrDefaultAsync(x => x.Id == id && x.CompanyDb == CompanyDb, cancellationToken);
        if (record == null) return NotFound(ApiResponse<object>.Fail("SYS-02", "Record not found"));
        var (success, operations) = await service.CancelOutgoingPayment(record);
        return Ok(ApiResponse<object>.Ok(new { success, operations }));
    }

    private static StageWisePaymentStages MapStage(string? type) => type switch
    {
        "Advance" => StageWisePaymentStages.AgainstPoAcceptance,
        "Running" => StageWisePaymentStages.AgainstReadinessOfMaterial,
        "Invoice" => StageWisePaymentStages.AfterReceiptOfMaterial,
        "Retention" => StageWisePaymentStages.AfterSuccessfulWorkCompletion,
        _ => StageWisePaymentStages.AgainstPoAcceptance
    };
}
