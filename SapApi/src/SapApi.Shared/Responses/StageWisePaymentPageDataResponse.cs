using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Responses;

public class StageWisePaymentPageDataResponse
{
    public SapPurchaseOrdersResponse? PurchaseOrder { get; set; }
    public string? ProjectName { get; set; }
    public double TotalBasic { get; set; }
    public double BalancePayment { get; set; }
    public List<PaymentTermsUdf> PaymentTerms { get; set; } = [];
    public List<StageWisePaymentRecordDto> TableRecords { get; set; } = [];
    public List<StageWisePaymentRecordDto> ActiveRecords { get; set; } = [];
    public List<StageWisePaymentBankOption> Banks { get; set; } = [];
    public Dictionary<string, string> BankLabels { get; set; } = [];
    public List<SapPurchaseInvoicesResponse> ApInvoices { get; set; } = [];
    public List<StageWisePaymentWtCodeOption> WithholdingTaxCodes { get; set; } = [];
    public List<StageWisePaymentSummaryRow> PaymentSummary { get; set; } = [];
}

public class StageWisePaymentRecordDto
{
    public int Id { get; set; }
    public int? PaymentTermsType { get; set; }
    public string? StageDesc { get; set; }
    public string? Bank { get; set; }
    public string? UtrNo { get; set; }
    public DateTime? UtrDate { get; set; }
    public string? ApprovalRequestId { get; set; }
    public string? ApInvoiceDocEntry { get; set; }
    public string? ApDownPaymentInvoiceEntryNumber { get; set; }
    public string? WtCode { get; set; }
    public double? GrossAmount { get; set; }
    public double? GstAmount { get; set; }
    public double? Tds { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? DocNumber { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }
}

public class StageWisePaymentBankOption
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class StageWisePaymentWtCodeOption
{
    public string WtCode { get; set; } = string.Empty;
    public string? WtName { get; set; }
    public double? Rate { get; set; }
}

public class StageWisePaymentSummaryRow
{
    public string Label { get; set; } = string.Empty;
    public double POValue { get; set; }
    public double Requested { get; set; }
    public double Paid { get; set; }
    public double Balance => POValue - Paid;
}
