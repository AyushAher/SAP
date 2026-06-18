using SapApi.Domain.Entities;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public static class StageWisePaymentCalculations
{
    public static List<StageWisePaymentSummaryRow> BuildPaymentSummary(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<StageWisePayment> activeRecords)
    {
        static bool IsPaid(StageWisePayment p) =>
            !string.IsNullOrEmpty(p.ApDownPaymentInvoiceEntryNumber)
            && p.Status is StageWisePaymentStatus.Approved or StageWisePaymentStatus.Added;

        var poBasic = (po.DocTotal ?? 0) - (po.VatSum ?? 0);
        var poTax = po.VatSum ?? 0;
        var poTotal = po.DocTotal ?? 0;

        var basicRequested = activeRecords.Sum(p => p.GrossAmount ?? 0);
        var taxRequested = activeRecords.Sum(p => p.GstAmount ?? 0);
        var basicPaid = activeRecords.Where(IsPaid).Sum(p => p.GrossAmount ?? 0);
        var taxPaid = activeRecords.Where(IsPaid).Sum(p => p.GstAmount ?? 0);

        return
        [
            new StageWisePaymentSummaryRow { Label = "Basic", POValue = poBasic, Requested = basicRequested, Paid = basicPaid },
            new StageWisePaymentSummaryRow { Label = "Tax", POValue = poTax, Requested = taxRequested, Paid = taxPaid },
            new StageWisePaymentSummaryRow { Label = "Total", POValue = poTotal, Requested = basicRequested + taxRequested, Paid = basicPaid + taxPaid },
        ];
    }

    public static double GetBalancePayment(SapPurchaseOrdersResponse po, IReadOnlyList<StageWisePayment> activeRecords) =>
        (po.DocTotal ?? 0) - activeRecords.Sum(x => (x.Tds ?? 0) + (x.GrossAmount ?? 0));

    public static double GetPayableAmount(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        PaymentTermsUdf? selectedTerm,
        int? paymentTermId = null,
        double totalBasic = 0)
    {
        var termId = paymentTermId ?? selectedTerm?.Id;
        var paymentTerm = paymentTerms.FirstOrDefault(x => x.Id == termId);
        if (paymentTerm is null)
            return 0;

        var alreadyPaid = activeRecords
            .Where(x => x.PaymentTermsType == paymentTerm.Id)
            .Sum(x => (x.GrossAmount ?? 0) + (x.GstAmount ?? 0));

        return Math.Round(
            (totalBasic * (paymentTerm.Basic ?? 0) / 100)
            + ((po.VatSum ?? 0) * (paymentTerm.Gst ?? 0) / 100)
            - alreadyPaid,
            2);
    }

    public static double GetAlreadyPaidAmountForPaymentTerms(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        PaymentTermsUdf? selectedTerm,
        int? paymentTermsType,
        double totalBasic)
    {
        var selectedPaymentTermPayable = GetPayableAmount(po, paymentTerms, activeRecords, selectedTerm, paymentTermsType, totalBasic);
        var negativePayableAmount = 0.0;
        var ids = new List<int>();

        foreach (var paymentTerm in paymentTerms)
        {
            if (selectedTerm?.Id == paymentTerm.Id)
                continue;

            var payableAmt = GetPayableAmount(po, paymentTerms, activeRecords, paymentTerm, paymentTerm.Id, totalBasic);
            if (payableAmt < 0 && paymentTerm.Id is not null)
            {
                negativePayableAmount += payableAmt;
                ids.Add(paymentTerm.Id.Value);
            }
        }

        if (ids.Contains(paymentTermsType ?? 0))
            return GetPayableAmount(po, paymentTerms, activeRecords, selectedTerm, paymentTermsType, totalBasic);

        if (ids.Count > 0 && (ids.Max() + 1) == paymentTermsType)
            return selectedPaymentTermPayable + negativePayableAmount;

        return selectedPaymentTermPayable;
    }

    public static double GetApInvoiceBalanceDue(
        SapPurchaseOrdersResponse? po,
        PaymentTermsUdf? selectedTerm,
        SapPurchaseInvoicesResponse? selectedApInvoice,
        IReadOnlyList<StageWisePayment> activeRecords,
        string? apInvoiceDocEntry)
    {
        if (po?.DocumentStatus != "bost_Close" && selectedTerm?.Type is not "Invoice" and not "Retention")
            return 0;

        var sapBalance = (selectedApInvoice?.DocTotal ?? 0)
            - (selectedApInvoice?.PaidToDate ?? 0)
            + (selectedApInvoice?.WTAmount ?? 0);

        var usedAmount = activeRecords
            .Where(x => x.ApInvoiceDocEntry == apInvoiceDocEntry
                && string.IsNullOrEmpty(x.ApDownPaymentInvoiceEntryNumber))
            .Sum(x => (x.GrossAmount ?? 0) + (x.GstAmount ?? 0));

        return Math.Round(sapBalance - usedAmount, 2);
    }

    public static double ResolvePayableForCreate(
        SapPurchaseOrdersResponse po,
        PaymentTermsUdf selectedTerm,
        IReadOnlyList<StageWisePayment> activeRecords,
        SapPurchaseInvoicesResponse? selectedApInvoice,
        string? apInvoiceDocEntry,
        double totalBasic)
    {
        if (selectedTerm.Type is "Invoice" or "Retention")
        {
            return GetApInvoiceBalanceDue(po, selectedTerm, selectedApInvoice, activeRecords, apInvoiceDocEntry);
        }

        return GetAlreadyPaidAmountForPaymentTerms(
            po, po.CreateUdfList(), activeRecords, selectedTerm, selectedTerm.Id, totalBasic);
    }
}
