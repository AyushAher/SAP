using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Helpers;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentPdfBuilder(SapMasterDataService masterDataService)
{
    public async Task<Dictionary<string, string>> BuildPlaceholdersAsync(
        StageWisePayment record,
        StageWisePaymentPageDataResponse pageData,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var po = pageData.PurchaseOrder!;
        var recordApInvoice = pageData.ApInvoices.FirstOrDefault(x => x.DocEntry.ToString() == record.ApInvoiceDocEntry)
            ?? pageData.ApInvoices.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(record.ApInvoiceDocEntry)
                && record.ApInvoiceDocEntry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(x.DocEntry?.ToString()));
        var paymentTerm = pageData.PaymentTerms.FirstOrDefault(x => x.Id == record.PaymentTermsType);
        var grossOutgoing = (record.GrossAmount ?? 0) + (record.GstAmount ?? 0) - (record.Tds ?? 0);
        var isBatchAp = record.StageDesc == "Batch AP payment";
        var isBatchDown = record.StageDesc == "Batch down payment";
        var paymentTypeLabel = isBatchAp || paymentTerm?.Type is "Invoice" or "Retention"
            ? "Outgoing Payment Request"
            : "Downpayment Request";

        var branch = await masterDataService.GetBusinessPlaceByIdAsync(po.BPLId, cancellationToken);

        var placeholders = new Dictionary<string, string>
        {
            ["gstTotal"] = (po.VatSum ?? 0).ToString("N2"),
            ["basicTotal"] = pageData.TotalBasic.ToString("N2"),
            ["grossAmount"] = ((record.GrossAmount ?? 0) + (record.GstAmount ?? 0)).ToString("N2"),
            ["grossTotal"] = (po.DocTotal ?? 0).ToString("N2"),
            ["outgoingPaymentValue"] = grossOutgoing.ToString("N2"),
            ["outgoingPaymentValueInWords"] = AmountInWords.ConvertToWords(grossOutgoing),
            ["paymentTerm"] = record.StageDesc ?? paymentTerm?.Desc ?? string.Empty,
            ["vendor"] = $"{po.CardCode} - {po.CardName}",
            ["documentNo"] = po.DocNum?.ToString() ?? string.Empty,
            ["documentDate"] = po.DocDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            ["projectName"] = pageData.ProjectName ?? string.Empty,
            ["projectNo"] = po.Project ?? string.Empty,
            ["reqId"] = record.ApprovalRequestId ?? string.Empty,
            ["reqDate"] = record.ApprovalRequestId is not null ? record.CreatedOn.ToString("dd/MM/yyyy") : string.Empty,
            ["totalQty"] = "0.00",
            ["totalLineGrandTotal"] = "0.00",
            ["journalRemarks"] = isBatchAp || isBatchDown ? "Batch payment request" : "Auto generated from system",
            ["bank"] = pageData.BankLabels.GetValueOrDefault(record.Bank ?? string.Empty, record.Bank ?? string.Empty),
            ["paymentType"] = paymentTypeLabel,
            ["apInvoiceDocEntry"] = recordApInvoice?.NumAtCard ?? string.Empty,
            ["apBalanceDue"] = ((recordApInvoice?.DocTotal ?? 0) - (recordApInvoice?.PaidToDate ?? 0)).ToString("N2"),
            ["bplName"] = branch?.BplName ?? string.Empty,
            ["bplAddr"] = branch?.Address ?? string.Empty,
            ["bplGst"] = branch?.FederalTaxID ?? string.Empty,
            ["bplPan"] = branch?.PanNo ?? string.Empty,
            ["userName"] = userName ?? string.Empty,
            ["wtCode"] = record.WtCode ?? "-",
            ["wtName"] = "-",
            ["wtAmount"] = (record.Tds ?? 0).ToString("N2"),
            ["wtRate"] = "-",
        };

        if (recordApInvoice?.WithholdingTaxDataCollection is { Count: > 0 } wtCollection)
        {
            var wt = wtCollection.FirstOrDefault();
            placeholders["wtCode"] = wt?.WtCode ?? "-";
            placeholders["wtName"] = wt?.WtName ?? "-";
            placeholders["wtAmount"] = (recordApInvoice.WTAmount ?? 0).ToString("N2");
            placeholders["wtRate"] = $"{wt?.Rate ?? 0:N2}%";
        }
        else if (!string.IsNullOrWhiteSpace(record.WtCode))
        {
            await ApplyWithholdingTaxPlaceholdersAsync(
                placeholders,
                record.WtCode,
                record.Tds ?? 0,
                pageData.WithholdingTaxCodes,
                cancellationToken);
        }

        return placeholders;
    }

    async Task ApplyWithholdingTaxPlaceholdersAsync(
        Dictionary<string, string> placeholders,
        string wtCode,
        double tdsAmount,
        IReadOnlyList<StageWisePaymentWtCodeOption> pageWtCodes,
        CancellationToken cancellationToken)
    {
        placeholders["wtCode"] = wtCode;
        placeholders["wtAmount"] = tdsAmount.ToString("N2");

        var wtFromPage = pageWtCodes.FirstOrDefault(w =>
            string.Equals(w.WtCode, wtCode, StringComparison.OrdinalIgnoreCase));
        if (wtFromPage is not null)
        {
            placeholders["wtName"] = wtFromPage.WtName ?? "-";
            placeholders["wtRate"] = wtFromPage.Rate.HasValue ? $"{wtFromPage.Rate.Value:N2}%" : "-";
            return;
        }

        var wtMaster = await masterDataService.GetWithholdingTaxByCodesAsync([wtCode], cancellationToken);
        var wt = wtMaster.FirstOrDefault(w =>
            string.Equals(w.WtCode, wtCode, StringComparison.OrdinalIgnoreCase));
        placeholders["wtName"] = wt?.WtName ?? "-";
        placeholders["wtRate"] = wt?.Rate.HasValue == true ? $"{wt.Rate.Value:N2}%" : "-";
    }
}
