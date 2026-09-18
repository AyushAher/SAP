using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Responses;

namespace SapApi.Infrastructure.Services.PaymentAdvice;

/// <summary>
/// Fills the vendor-facing "Payment Advice" email template (Templates/Payment_Advice_Template.html)
/// with a stage-wise payment's placeholder values and renders it. Owns nothing about *when* to send
/// — callers gate that on <see cref="StageWisePaymentPageService.HasSapOutgoingPayment"/>.
/// </summary>
public class PaymentAdviceService(SapMasterDataService masterDataService, IPdfService pdfService)
{
    private const string TemplateName = "Payment_Advice_Template.html";

    public async Task<Dictionary<string, string>> BuildPlaceholdersAsync(
        StageWisePayment record,
        StageWisePaymentPageDataResponse pageData,
        string? vendorBankCode,
        DateTime? paymentDate,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var po = pageData.PurchaseOrder
            ?? throw new InvalidOperationException("Payment advice requires a loaded purchase order.");

        var branch = await masterDataService.GetBusinessPlaceByIdAsync(po.BPLId, cancellationToken: cancellationToken);
        var bankAccount = SelectVendorBankAccount(pageData.VendorBankAccounts, vendorBankCode);

        var gross = (record.GrossAmount ?? 0) + (record.GstAmount ?? 0);
        var tds = record.Tds ?? 0;
        var net = gross - tds;

        return new Dictionary<string, string>
        {
            ["CompanyName"] = branch?.BplName ?? string.Empty,
            ["VendorCode"] = po.CardCode ?? string.Empty,
            ["VendorName"] = po.CardName ?? string.Empty,
            ["PurchaseOrder"] = po.DocNum?.ToString() ?? po.DocEntry?.ToString() ?? string.Empty,
            ["BankName"] = bankAccount?.BankCode ?? string.Empty,
            ["AccountNumber"] = bankAccount?.AccountNo ?? string.Empty,
            // Not tracked anywhere in the SAP BP bank master this tenant syncs — left blank rather
            // than guessed from an unrelated field (e.g. BIC/SWIFT, which is a different code).
            ["IFSCCode"] = string.Empty,
            ["BranchName"] = bankAccount?.Branch ?? string.Empty,
            ["GrossAmount"] = gross.ToString("N2"),
            ["TDSAmount"] = tds.ToString("N2"),
            ["NetAmount"] = net.ToString("N2"),
            ["PaymentDate"] = (paymentDate ?? record.UtrDate)?.ToString("dd/MM/yyyy") ?? string.Empty,
            ["BankReferenceNo"] = record.UtrNo ?? string.Empty,
            ["Remarks"] = remarks ?? string.Empty,
        };
    }

    public Task<string> RenderHtmlAsync(
        IDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default) =>
        pdfService.RenderTemplateHtmlAsync(TemplateName, placeholders, cancellationToken);

    /// <summary>
    /// A vendor may have several bank accounts on file — show only the one the payment actually
    /// used (<paramref name="vendorBankCode"/>) so the advice never names the wrong account. Falls
    /// back to the vendor's first account on file when no selection is recorded (e.g. a single,
    /// non-batch payment, which carries no VendorBankCode of its own).
    /// </summary>
    private static VendorBankAccountOption? SelectVendorBankAccount(
        IEnumerable<VendorBankAccountOption>? accounts,
        string? vendorBankCode)
    {
        var list = accounts?.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(vendorBankCode))
        {
            var selected = list.FirstOrDefault(a => a.BankCode == vendorBankCode);
            if (selected is not null)
                return selected;
        }

        return list.FirstOrDefault();
    }
}
