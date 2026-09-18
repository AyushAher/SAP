namespace SapApi.Shared.Requests;

public class SendPaymentAdviceRequest
{
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    /// <summary>Free-text note dropped into the template's Remarks row — not the whole email body,
    /// which is system-generated from Payment_Advice_Template.html.</summary>
    public string? Remarks { get; set; }
}
