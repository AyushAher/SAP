namespace SapApi.Shared.Responses;

public class StageWisePaymentBatchResponse
{
    public int Id { get; set; }
    public int PoDocEntry { get; set; }
    public int? DocNumber { get; set; }
    public int? StageWisePaymentId { get; set; }
    public int? DownPaymentStageWisePaymentId { get; set; }
    public string? ApprovalRequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanEditAdditionalDetails { get; set; }
    public bool HasSapOutgoingPayment { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool IsLastApproval { get; set; }
    public int? ApprovalRequestIdNumeric { get; set; }
    public string? WtCode { get; set; }
    public string? ModeOfPayment { get; set; }
    public string? ModeOfPaymentLabel { get; set; }
    public string? Account { get; set; }
    public string? AccountLabel { get; set; }
    public string? JournalRemark { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime? PostingDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public List<StageWisePaymentBatchLineResponse> Lines { get; set; } = [];
}

public class StageWisePaymentBatchLineResponse
{
    public int Id { get; set; }
    public string? ApInvoiceDocEntry { get; set; }
    public string? ApInvoiceLabel { get; set; }
    public List<int> PaymentTermsTypes { get; set; } = [];
    public List<string> PaymentTermLabels { get; set; } = [];
    public string? Bank { get; set; }
    public string? WtCode { get; set; }
    public double Amount { get; set; }
    public double BalanceDue { get; set; }
    public double Payable { get; set; }
    public string? Notes { get; set; }
}

public class CalculateBatchLineResponse
{
    public double BalanceDue { get; set; }
    public double Payable { get; set; }
}
