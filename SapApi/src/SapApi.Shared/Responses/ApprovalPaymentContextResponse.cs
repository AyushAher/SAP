namespace SapApi.Shared.Responses;

public class ApprovalPaymentContextResponse
{
    public string? VendorDisplay { get; set; }
    public string? PoDetails { get; set; }
    public string? ProjectName { get; set; }
    public string? BankAccount { get; set; }
    public string? Branch { get; set; }
    public double? TransferAmount { get; set; }
    public string? UtrNo { get; set; }
    public DateTime? UtrDate { get; set; }
    public List<ApprovalTimelineItemDto> PreviousApprovals { get; set; } = [];
    public List<StageWisePaymentSummaryItemDto> StageWisePayments { get; set; } = [];
    public List<PaymentTermSummaryItemDto> PaymentTerms { get; set; } = [];
}

public class ApprovalTimelineItemDto
{
    public string? ApproverName { get; set; }
    public DateTime? ActionDate { get; set; }
    public string? Comment { get; set; }
    public string? Status { get; set; }
}

public class StageWisePaymentSummaryItemDto
{
    public string? RequestId { get; set; }
    public string? PaymentStage { get; set; }
    public double NetBasicAmount { get; set; }
    public double TdsAmount { get; set; }
    public double GstAmount { get; set; }
    public double GrossAmount { get; set; }
    public string? Status { get; set; }
    public bool IsTotalRow { get; set; }
}

public class PaymentTermSummaryItemDto
{
    public int? Id { get; set; }
    public string? Desc { get; set; }
    public string? Type { get; set; }
}
