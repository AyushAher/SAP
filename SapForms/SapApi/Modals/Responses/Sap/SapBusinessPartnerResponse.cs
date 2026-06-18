namespace SapApi.Modals.Responses.Sap
{
    public class SapBusinessPartnerResponse : SapError
    {
        [JsonPropertyName("value")] public List<SapBusinessPartner>? Value { get; set; }
    }
    public record SapBusinessPartner
    {
        [JsonPropertyName("CardCode")] public string? CardCode { get; set; }
        [JsonPropertyName("CardName")] public string? CardName { get; set; }
        [JsonPropertyName("CardType")] public string? CardType { get; set; }
        [JsonPropertyName("GroupCode")] public int? GroupCode { get; set; }
    }
}
