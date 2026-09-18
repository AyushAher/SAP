using SapApi.Domain.Entities;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

/// <summary>
/// SAP down payments and outgoing payments use independent numbering series, so DocNum 123
/// on a PurchaseDownPayment is not the same document as VendorPayments DocNum 123.
/// Cancel by DocEntry (and document type) whenever those ids are stored.
/// </summary>
public sealed record StageWisePaymentCancelPlan(
    IReadOnlyList<string> VendorPaymentDocEntries,
    IReadOnlyList<string> DownPaymentDocEntries,
    IReadOnlyList<string> VendorPaymentDocNums,
    IReadOnlyList<string> DownPaymentDocNums)
{
    public bool HasSapDocuments =>
        VendorPaymentDocEntries.Count > 0
        || DownPaymentDocEntries.Count > 0
        || VendorPaymentDocNums.Count > 0
        || DownPaymentDocNums.Count > 0;
}

public static class StageWisePaymentCancelPlanner
{
    public const string BatchApPaymentStage = "Batch AP payment";
    public const string BatchDownPaymentStage = "Batch down payment";

    public static StageWisePaymentCancelPlan Build(StageWisePayment record)
    {
        var vendorEntries = SplitLinkedDocs(record.PaymentDocEntry);
        var downEntries = SplitLinkedDocs(record.DownPaymentDocEntry)
            .Concat(SplitLinkedDocs(record.ApDownPaymentInvoiceDocEntry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // DocNums are a legacy fallback only. Never use them when DocEntry is already known —
        // looking up VendorPayments by a down-payment DocNum cancels an unrelated outgoing payment.
        if (vendorEntries.Count > 0 || downEntries.Count > 0)
        {
            return new StageWisePaymentCancelPlan(vendorEntries, downEntries, [], []);
        }

        var docNums = SplitLinkedDocs(record.ApDownPaymentInvoiceEntryNumber);
        if (docNums.Count == 0)
            return new StageWisePaymentCancelPlan([], [], [], []);

        if (IsOutgoingOnly(record))
            return new StageWisePaymentCancelPlan([], [], docNums, []);

        if (docNums.Count == 1)
            return new StageWisePaymentCancelPlan([], [], [], docNums);

        // ApplyOutgoingPaymentResult appends the outgoing DocNum last after down-payment DocNums.
        var downNums = docNums.Take(docNums.Count - 1).ToList();
        var vendorNums = new[] { docNums[^1] };
        return new StageWisePaymentCancelPlan([], [], vendorNums, downNums);
    }

    public static List<string> SplitLinkedDocs(string? value) =>
        value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    private static bool IsOutgoingOnly(StageWisePayment record) =>
        string.Equals(record.StageDesc, BatchApPaymentStage, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the SAP outgoing payment is applied to this request's down payment or AP invoice.
    /// Same DocNum on an unrelated VendorPayment is not enough.
    /// </summary>
    public static bool IsOutgoingPaymentLinkedToRecord(
        StageWisePayment record,
        SapVendorPaymentsResponse payment)
    {
        if (payment.DocEntry is null)
            return false;

        var downEntries = ParseDocEntries(record.DownPaymentDocEntry, record.ApDownPaymentInvoiceDocEntry);
        var apEntries = ParseDocEntries(record.ApInvoiceDocEntry);
        var lines = payment.PaymentInvoices ?? [];

        if (lines.Count > 0)
        {
            if (downEntries.Count > 0 && lines.Any(line =>
                    LineMatches(line, downEntries, Constants.SapVendorPaymentInvoiceType.DownPayment)))
                return true;

            if (apEntries.Count > 0 && lines.Any(line =>
                    LineMatches(line, apEntries, Constants.SapVendorPaymentInvoiceType.Invoice)))
                return true;

            return false;
        }

        // Service Layer omitted invoice lines — only trust the DocEntry we stored when posting.
        var storedOutgoing = SplitLinkedDocs(record.PaymentDocEntry);
        return storedOutgoing.Contains(payment.DocEntry.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<int> ParseDocEntries(params string?[] values)
    {
        var entries = new HashSet<int>();
        foreach (var value in values)
        {
            foreach (var token in SplitLinkedDocs(value))
            {
                if (int.TryParse(token, out var id))
                    entries.Add(id);
            }
        }

        return entries;
    }

    private static bool LineMatches(PaymentInvoice line, HashSet<int> expectedEntries, string expectedType)
    {
        if (line.DocEntry is not int docEntry || !expectedEntries.Contains(docEntry))
            return false;
        if (string.IsNullOrWhiteSpace(line.InvoiceType))
            return true;
        return string.Equals(line.InvoiceType, expectedType, StringComparison.OrdinalIgnoreCase);
    }
}
