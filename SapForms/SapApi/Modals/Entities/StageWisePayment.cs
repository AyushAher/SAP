using SapApi.Modals.Responses.Sap;

namespace SapApi.Modals.Entities
{
    public class StageWisePayment
    {
        public int Id { get; set; }
        public int? PaymentTermsType { get; set; }
        public StageWisePaymentStages Stage { get; set; }
        public string? ApDownPaymentInvoiceEntryNumber { get; set; }
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
        Added
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