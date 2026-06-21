using System.ComponentModel.DataAnnotations.Schema;
namespace SapApi.Domain.Entities
{
    public class StageWisePayment
    {
        public int Id { get; set; }
        public string CompanyDb { get; set; } = string.Empty;
        public int? PaymentTermsType { get; set; }
        public StageWisePaymentStages Stage { get; set; }
        public string? StageDesc { get; set; }
        public string? Bank { get; set; }
        public string? UtrNo { get; set; }
        public DateTime? UtrDate { get; set; }
        public string? ApprovalRequestId { get; set; }
        public string? ApInvoiceDocEntry { get; set; }
        public string? ApDownPaymentInvoiceEntryNumber { get; set; }
        [NotMapped]
        public string? ApDownPaymentInvoiceDocEntry { get; set; }
        public string? WtCode { get; set; }
        public double? GrossAmount { get; set; }
        public double? GstAmount { get; set; }
        public double? Tds { get; set; }
        public StageWisePaymentStatus Status { get; set; } = StageWisePaymentStatus.Added;
        public int? DocNumber { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastModifiedOn { get; set; }
    }

    public enum StageWisePaymentStatus
    {
        Added,
        PendingApproval,
        Approved,
        Cancelled
    }

    public enum StageWisePaymentStages
    {
        AgainstPoAcceptance,
        AgainstDrawingApproval,
        AgainstDocumentApproval,
        AgainstSubmissionOfCld,
        AgainstReadinessOfMaterial,
        AfterReceiptOfMaterial,
        AfterSuccessfulWorkCompletion
    }
}