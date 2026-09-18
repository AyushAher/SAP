namespace SapApi.Shared.Responses;

/// <summary>One line-item row of the Purchase Request report PDF.</summary>
public class PurchaseRequestReportLineResponse
{
    public int DocNum { get; set; }
    public DateTime? PostingDate { get; set; }
    public string? UserName { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? ProjectCode { get; set; }
    public string? ItemType { get; set; }
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public string? FreeText { get; set; }
    public double? Quantity { get; set; }
    public string? Unit { get; set; }
}
