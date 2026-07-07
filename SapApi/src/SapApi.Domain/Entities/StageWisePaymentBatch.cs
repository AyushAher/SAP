namespace SapApi.Domain.Entities;

public class StageWisePaymentBatch
{
    public int Id { get; set; }
    public string CompanyDb { get; set; } = string.Empty;
    public int PoDocEntry { get; set; }
    public int? DocNumber { get; set; }
    public int? StageWisePaymentId { get; set; }
    public int? DownPaymentStageWisePaymentId { get; set; }
    public string? ApprovalRequestId { get; set; }
    public string? WtCode { get; set; }
    public string? ModeOfPayment { get; set; }
    public string? Account { get; set; }
    public string? JournalRemark { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime? PostingDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public StageWisePaymentBatchStatus Status { get; set; } = StageWisePaymentBatchStatus.Draft;
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }

    public StageWisePayment? StageWisePayment { get; set; }
    public StageWisePayment? DownPaymentStageWisePayment { get; set; }
    public ICollection<StageWisePaymentBatchLine> Lines { get; set; } = [];
}

public enum StageWisePaymentBatchStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled,
}

public class StageWisePaymentBatchLine
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string? ApInvoiceDocEntry { get; set; }
    public string? Bank { get; set; }
    public string? WtCode { get; set; }
    public double Amount { get; set; }
    public double? BalanceDue { get; set; }
    public double? Payable { get; set; }
    public int LineOrder { get; set; }
    public string? Notes { get; set; }

    public StageWisePaymentBatch Batch { get; set; } = null!;
    public ICollection<StageWisePaymentBatchLinePaymentTerm> PaymentTerms { get; set; } = [];
}

public class StageWisePaymentBatchLinePaymentTerm
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public int PaymentTermsType { get; set; }
    public string? PaymentTermDesc { get; set; }

    public StageWisePaymentBatchLine Line { get; set; } = null!;
}
