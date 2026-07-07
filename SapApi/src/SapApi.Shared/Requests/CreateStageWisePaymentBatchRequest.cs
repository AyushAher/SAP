namespace SapApi.Shared.Requests;

public class CreateStageWisePaymentBatchRequest
{
    public int PoDocEntry { get; set; }
    public int? DocNumber { get; set; }
    public string? WtCode { get; set; }
    public string? ModeOfPayment { get; set; }
    public string? Account { get; set; }
    public string? JournalRemark { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime? PostingDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public List<StageWisePaymentBatchLineRequest> Lines { get; set; } = [];
}

public class StageWisePaymentBatchLineRequest
{
    public string? ApInvoiceDocEntry { get; set; }
    public List<int> PaymentTermsTypes { get; set; } = [];
    public string? Bank { get; set; }
    public string? WtCode { get; set; }
    public double Amount { get; set; }
    public string? Notes { get; set; }
}

public class CalculateBatchLineRequest
{
    public int PoDocEntry { get; set; }
    public string? ApInvoiceDocEntry { get; set; }
    public List<int> PaymentTermsTypes { get; set; } = [];
    public int? ExcludeBatchId { get; set; }
}
