using SapApi.Domain.Entities;
using SapApi.Shared.Requests;
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
        if (po.DocumentStatus == "bost_Close" && selectedTerm.Type is "Invoice" or "Retention")
        {
            return GetApInvoiceBalanceDue(po, selectedTerm, selectedApInvoice, activeRecords, apInvoiceDocEntry);
        }

        return GetAlreadyPaidAmountForPaymentTerms(
            po, po.CreateUdfList(), activeRecords, selectedTerm, selectedTerm.Id, totalBasic);
    }

    public static double ResolveBatchRowPayable(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        IReadOnlyList<int> selectedTermIds,
        SapPurchaseInvoicesResponse? apInvoice,
        string? apInvoiceDocEntry,
        double totalBasic)
    {
        if (selectedTermIds.Count == 0)
            return 0;

        if (ShouldUseApInvoiceBalanceForPayable(po, paymentTerms, selectedTermIds, apInvoiceDocEntry))
        {
            var representativeTerm = selectedTermIds
                .Select(id => paymentTerms.FirstOrDefault(t => t.Id == id))
                .FirstOrDefault(t => t?.Type is "Invoice" or "Retention")
                ?? selectedTermIds.Select(id => paymentTerms.FirstOrDefault(t => t.Id == id)).FirstOrDefault(t => t is not null);

            return GetApInvoiceBalanceDue(po, representativeTerm, apInvoice, activeRecords, apInvoiceDocEntry);
        }

        var sum = selectedTermIds.Distinct().Sum(termId =>
        {
            var term = paymentTerms.FirstOrDefault(t => t.Id == termId);
            return term is null
                ? 0
                : GetAlreadyPaidAmountForPaymentTerms(po, paymentTerms, activeRecords, term, termId, totalBasic);
        });

        return Math.Round(sum, 2);
    }

    private static bool ShouldUseApInvoiceBalanceForPayable(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<int> selectedTermIds,
        string? apInvoiceDocEntry)
    {
        var hasInvoiceRetentionTerm = selectedTermIds.Any(id =>
            paymentTerms.FirstOrDefault(t => t.Id == id)?.Type is "Invoice" or "Retention");

        if (po.DocumentStatus == "bost_Close" && hasInvoiceRetentionTerm)
            return true;

        if (string.IsNullOrWhiteSpace(apInvoiceDocEntry))
            return false;

        return po.DocumentStatus == "bost_Close" || hasInvoiceRetentionTerm;
    }

    /// <summary>
    /// AP invoice / vendor outgoing payments must be created via batch payment only.
    /// </summary>
    public static bool RequiresBatchPayment(
        SapPurchaseOrdersResponse po,
        PaymentTermsUdf? paymentTerm,
        string? apInvoiceDocEntry = null) =>
        po.DocumentStatus == "bost_Close"
        || paymentTerm?.Type is "Invoice" or "Retention"
        || !string.IsNullOrWhiteSpace(apInvoiceDocEntry);

    public static bool IsSinglePaymentAllowed(SapPurchaseOrdersResponse po, PaymentTermsUdf paymentTerm) =>
        !RequiresBatchPayment(po, paymentTerm);

    /// <summary>
    /// Batch rows against closed POs or Invoice/Retention terms require an AP invoice (vendor outgoing payment).
    /// Open PO advance/down-payment terms use purchase down payment and do not require AP invoices.
    /// </summary>
    public static bool BatchRowRequiresApInvoice(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<int> selectedTermIds) =>
        po.DocumentStatus == "bost_Close"
        || selectedTermIds.Any(id => paymentTerms.FirstOrDefault(t => t.Id == id)?.Type is "Invoice" or "Retention");

    public static PaymentTermsUdf? ResolveDownPaymentTerm(
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<int> selectedTermIds) =>
        selectedTermIds
            .Select(id => paymentTerms.FirstOrDefault(t => t.Id == id))
            .FirstOrDefault(t => t?.Type is not "Invoice" and not "Retention")
        ?? selectedTermIds.Select(id => paymentTerms.FirstOrDefault(t => t.Id == id)).FirstOrDefault(t => t is not null);

    public static (double BalanceDue, double Payable) ResolveBatchRowAmounts(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        IReadOnlyList<int> selectedTermIds,
        SapPurchaseInvoicesResponse? apInvoice,
        string? apInvoiceDocEntry,
        double totalBasic)
    {
        if (selectedTermIds.Count == 0)
            return (0, 0);

        var representativeTerm = selectedTermIds
            .Select(id => paymentTerms.FirstOrDefault(t => t.Id == id))
            .FirstOrDefault(t => t?.Type is "Invoice" or "Retention")
            ?? selectedTermIds.Select(id => paymentTerms.FirstOrDefault(t => t.Id == id)).FirstOrDefault(t => t is not null);

        var balanceDue = ShouldUseApInvoiceBalanceForPayable(po, paymentTerms, selectedTermIds, apInvoiceDocEntry)
            ? GetApInvoiceBalanceDue(po, representativeTerm, apInvoice, activeRecords, apInvoiceDocEntry)
            : 0;

        if (ShouldUseApInvoiceBalanceForPayable(po, paymentTerms, selectedTermIds, apInvoiceDocEntry))
            return (balanceDue, balanceDue);

        var payable = ResolveBatchRowPayable(
            po, paymentTerms, activeRecords, selectedTermIds, apInvoice, apInvoiceDocEntry, totalBasic);

        return (0, payable);
    }

    public static double GetPaymentTermPayable(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        int termId,
        double totalBasic)
    {
        var term = paymentTerms.FirstOrDefault(t => t.Id == termId);
        return term is null
            ? 0
            : GetAlreadyPaidAmountForPaymentTerms(po, paymentTerms, activeRecords, term, termId, totalBasic);
    }

    public static Dictionary<int, double> AllocateRowAmountByPaymentTerm(
        double amount,
        IReadOnlyList<int> termIds,
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        double totalBasic)
    {
        var uniqueTermIds = termIds.Distinct().ToList();
        var allocations = new Dictionary<int, double>();
        if (uniqueTermIds.Count == 0 || amount <= 0)
            return allocations;

        var weights = uniqueTermIds
            .Select(termId => (termId, Weight: Math.Max(0, GetPaymentTermPayable(po, paymentTerms, activeRecords, termId, totalBasic))))
            .ToList();
        var totalWeight = weights.Sum(w => w.Weight);

        if (totalWeight <= 0)
        {
            var evenShare = Math.Round(amount / uniqueTermIds.Count, 2);
            foreach (var termId in uniqueTermIds)
                allocations[termId] = evenShare;
            return allocations;
        }

        var allocated = 0.0;
        for (var i = 0; i < weights.Count; i++)
        {
            var (termId, weight) = weights[i];
            if (i == weights.Count - 1)
            {
                allocations[termId] = Math.Round(amount - allocated, 2);
                continue;
            }

            var share = Math.Round(amount * weight / totalWeight, 2);
            allocations[termId] = share;
            allocated += share;
        }

        return allocations;
    }

    public static double GetPriorBatchAllocatedAmountForPaymentTerm(
        IReadOnlyList<StageWisePaymentBatchLineRequest> priorLines,
        int termId,
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        double totalBasic) =>
        priorLines.Sum(line =>
        {
            if (line.Amount <= 0 || !line.PaymentTermsTypes.Contains(termId))
                return 0.0;

            var allocations = AllocateRowAmountByPaymentTerm(
                line.Amount, line.PaymentTermsTypes, po, paymentTerms, activeRecords, totalBasic);
            return allocations.GetValueOrDefault(termId, 0);
        });

    public static double ComputeSequentialStageRowPayable(
        StageWisePaymentBatchLineRequest line,
        IReadOnlyList<StageWisePaymentBatchLineRequest> priorLines,
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        double totalBasic)
    {
        if (line.PaymentTermsTypes.Count == 0)
            return 0;

        return Math.Round(line.PaymentTermsTypes.Distinct().Sum(termId =>
        {
            var termPayable = GetPaymentTermPayable(po, paymentTerms, activeRecords, termId, totalBasic);
            var priorAllocated = GetPriorBatchAllocatedAmountForPaymentTerm(
                priorLines, termId, po, paymentTerms, activeRecords, totalBasic);
            return Math.Max(0, termPayable - priorAllocated);
        }), 2);
    }

    public static (bool IsValid, string Message) ValidateBatchComposition(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePaymentBatchLineRequest> lines)
    {
        var hasApRows = false;
        var hasDownPaymentRows = false;

        foreach (var line in lines)
        {
            if (line.PaymentTermsTypes.Count == 0)
                continue;

            if (BatchRowRequiresApInvoice(po, paymentTerms, line.PaymentTermsTypes))
                hasApRows = true;
            else
                hasDownPaymentRows = true;
        }

        if (hasApRows && hasDownPaymentRows)
        {
            return (false,
                "A batch cannot mix AP invoice payments with down payment stages. Create separate batches for each payment type so only one SAP document is created per batch.");
        }

        return (true, string.Empty);
    }

    public static (bool IsValid, string Message) ValidateBatchLineAmounts(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<StageWisePayment> activeRecords,
        IReadOnlyList<StageWisePaymentBatchLineRequest> lines,
        double totalBasic,
        IReadOnlyDictionary<string, SapPurchaseInvoicesResponse> apInvoicesByDocEntry)
    {
        var appliedByApInvoice = new Dictionary<string, double>(StringComparer.Ordinal);
        var priorLines = new List<StageWisePaymentBatchLineRequest>();
        var allocatedByTerm = new Dictionary<int, double>();

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.PaymentTermsTypes.Count == 0)
                return (false, "Each row must have at least one payment type selected.");

            if (line.Amount <= 0)
                return (false, "Each row amount must be greater than zero.");

            var requiresAp = BatchRowRequiresApInvoice(po, paymentTerms, line.PaymentTermsTypes);
            double adjustedPayable;

            if (requiresAp)
            {
                if (string.IsNullOrWhiteSpace(line.ApInvoiceDocEntry))
                    return (false, "AP invoice is required for Invoice, Retention, or closed PO payment rows.");

                if (!apInvoicesByDocEntry.TryGetValue(line.ApInvoiceDocEntry, out var apInvoice) || apInvoice.DocEntry is null)
                    return (false, $"AP invoice {line.ApInvoiceDocEntry} not found.");

                var (_, payable) = ResolveBatchRowAmounts(
                    po, paymentTerms, activeRecords, line.PaymentTermsTypes, apInvoice,
                    line.ApInvoiceDocEntry, totalBasic);

                var priorInBatch = appliedByApInvoice.GetValueOrDefault(line.ApInvoiceDocEntry, 0);
                adjustedPayable = Math.Round(Math.Max(0, payable - priorInBatch), 2);
                appliedByApInvoice[line.ApInvoiceDocEntry] = priorInBatch + line.Amount;
            }
            else
            {
                adjustedPayable = ComputeSequentialStageRowPayable(
                    line, priorLines, po, paymentTerms, activeRecords, totalBasic);

                var allocations = AllocateRowAmountByPaymentTerm(
                    line.Amount, line.PaymentTermsTypes, po, paymentTerms, activeRecords, totalBasic);
                foreach (var (termId, allocated) in allocations)
                    allocatedByTerm[termId] = allocatedByTerm.GetValueOrDefault(termId, 0) + allocated;
            }

            if (line.Amount > adjustedPayable)
            {
                var rowContext = FormatBatchLineContextLabel(line, paymentTerms, apInvoicesByDocEntry);
                return (false, $"Net amount cannot exceed payable ({adjustedPayable:N2}) for row {lineIndex + 1} ({rowContext}).");
            }

            priorLines.Add(line);
        }

        foreach (var (termId, allocated) in allocatedByTerm)
        {
            var payable = GetPaymentTermPayable(po, paymentTerms, activeRecords, termId, totalBasic);
            if (allocated > payable)
            {
                var term = paymentTerms.FirstOrDefault(t => t.Id == termId);
                var label = term?.Desc ?? term?.DropDownValue() ?? $"Payment type {termId}";
                return (false, $"Total net amount for {label} ({allocated:N2}) exceeds payable ({payable:N2}).");
            }
        }

        return (true, string.Empty);
    }

    private static string FormatBatchLineContextLabel(
        StageWisePaymentBatchLineRequest line,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyDictionary<string, SapPurchaseInvoicesResponse> apInvoicesByDocEntry)
    {
        var parts = new List<string>();

        var stageLabels = line.PaymentTermsTypes
            .Distinct()
            .Select(id => paymentTerms.FirstOrDefault(t => t.Id == id))
            .Where(t => t is not null)
            .Select(t => t!.Desc ?? t.DropDownValue())
            .ToList();

        if (stageLabels.Count > 0)
            parts.Add($"payment stage: {string.Join(", ", stageLabels)}");

        if (!string.IsNullOrWhiteSpace(line.ApInvoiceDocEntry))
        {
            var apLabel = line.ApInvoiceDocEntry;
            if (apInvoicesByDocEntry.TryGetValue(line.ApInvoiceDocEntry, out var apInvoice))
                apLabel = $"{apInvoice.DocNum}:{apInvoice.NumAtCard}";
            parts.Add($"AP invoice: {apLabel}");
        }

        return parts.Count == 0 ? "row" : string.Join("; ", parts);
    }

    public static (double GrossAmount, double GstAmount) SplitAmountForPaymentTerm(
        SapPurchaseOrdersResponse po,
        PaymentTermsUdf term,
        double amount,
        double totalBasic,
        IReadOnlyList<StageWisePayment> activeRecords)
    {
        if (amount <= 0)
            return (0, 0);

        if (po.DocumentStatus == "bost_Close" || term.Type is "Invoice" or "Retention")
            return (Math.Round(amount, 2), 0);

        var paidBasic = activeRecords
            .Where(x => x.PaymentTermsType == term.Id)
            .Sum(x => x.GrossAmount ?? 0);
        var remainingBasic = Math.Max(0, (totalBasic * (term.Basic ?? 0) / 100) - paidBasic);

        if (term.Basic is > 0)
        {
            if (term.Gst is > 0 && amount > remainingBasic)
                return (Math.Round(remainingBasic, 2), Math.Round(amount - remainingBasic, 2));

            return (Math.Round(amount, 2), 0);
        }

        if (term.Gst is > 0)
            return (0, Math.Round(amount, 2));

        return (Math.Round(amount, 2), 0);
    }

    public static (double GrossAmount, double GstAmount) SplitBatchLineAmount(
        SapPurchaseOrdersResponse po,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        IReadOnlyList<int> termIds,
        double amount,
        double totalBasic,
        IReadOnlyList<StageWisePayment> activeRecords)
    {
        var distinctTermIds = termIds.Distinct().ToList();
        if (distinctTermIds.Count == 0 || amount <= 0)
            return (0, 0);

        if (BatchRowRequiresApInvoice(po, paymentTerms, distinctTermIds))
            return (Math.Round(amount, 2), 0);

        if (distinctTermIds.Count == 1)
        {
            var term = paymentTerms.FirstOrDefault(t => t.Id == distinctTermIds[0]);
            return term is null
                ? (Math.Round(amount, 2), 0)
                : SplitAmountForPaymentTerm(po, term, amount, totalBasic, activeRecords);
        }

        var weights = distinctTermIds
            .Select(termId => (termId, Weight: Math.Max(0, GetPaymentTermPayable(po, paymentTerms, activeRecords, termId, totalBasic))))
            .ToList();
        var totalWeight = weights.Sum(w => w.Weight);

        if (totalWeight <= 0)
        {
            var evenShare = Math.Round(amount / distinctTermIds.Count, 2);
            return weights.Select((w, index) =>
            {
                var share = index == weights.Count - 1
                    ? Math.Round(amount - (evenShare * (weights.Count - 1)), 2)
                    : evenShare;
                var term = paymentTerms.FirstOrDefault(t => t.Id == w.termId);
                return term is null ? (share, 0d) : SplitAmountForPaymentTerm(po, term, share, totalBasic, activeRecords);
            }).Aggregate((g: 0d, gst: 0d), (acc, split) => (acc.g + split.Item1, acc.gst + split.Item2));
        }

        var allocated = 0.0;
        var grossTotal = 0.0;
        var gstTotal = 0.0;
        for (var i = 0; i < weights.Count; i++)
        {
            var (termId, weight) = weights[i];
            var term = paymentTerms.FirstOrDefault(t => t.Id == termId);
            if (term is null)
                continue;

            var share = i == weights.Count - 1
                ? Math.Round(amount - allocated, 2)
                : Math.Round(amount * weight / totalWeight, 2);
            allocated += share;

            var (gross, gst) = SplitAmountForPaymentTerm(po, term, share, totalBasic, activeRecords);
            grossTotal += gross;
            gstTotal += gst;
        }

        return (Math.Round(grossTotal, 2), Math.Round(gstTotal, 2));
    }

    public static double ComputeApInvoiceTdsAmount(
        SapPurchaseInvoicesResponse apInvoice,
        IReadOnlyList<StageWisePayment> activeRecords,
        string apInvoiceDocEntry,
        ISet<string> tdsAppliedApInvoices)
    {
        var hadTdsDeducted = activeRecords.Any(x =>
            x.ApInvoiceDocEntry == apInvoiceDocEntry && (x.Tds ?? 0) != 0);
        if (hadTdsDeducted || !tdsAppliedApInvoices.Add(apInvoiceDocEntry))
            return 0;

        return apInvoice.WTAmount ?? 0;
    }
}
