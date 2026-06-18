using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests;

public class CreateStageWisePaymentApiRequest
{
    public int? PaymentTermsType { get; set; }
    public string? StageDesc { get; set; }
    public string? Bank { get; set; }
    public string? ApInvoiceDocEntry { get; set; }
    public int PoDocEntry { get; set; }
    public int? DocNumber { get; set; }
    public PaymentTermsUdf SelectedPaymentTermsUdf { get; set; } = new();
    public double DownPaymentAmount { get; set; }
    public string? WtCode { get; set; }
    public string? Desc { get; set; }
}
