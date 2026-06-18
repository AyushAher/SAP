namespace Shared.Responses.Sap
{
    public class SapBusinessPartnerResponse : SapError
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapBusinessPartner>? Value { get; set; }
    }

    public record WithholdingTaxDataCollectionResponse : SapBaseResponse
    {
        [JsonPropertyName("WTName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WtName { get; set; }
        [JsonPropertyName("WTCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WtCode { get; set; }
        [JsonPropertyName("Rate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Rate { get; set; }
    }

    public record GetAllWithholdingTaxDataCollectionResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<WithholdingTaxDataCollectionResponse>? Value { get; set; }
    }

    public record SapBusinessPartner
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardCode { get; set; }
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardName { get; set; }
        [JsonPropertyName("CardType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardType { get; set; }
        [JsonPropertyName("GroupCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? GroupCode { get; set; }
        [JsonPropertyName("BPWithholdingTaxCollection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapWithholdingTaxDataCollectionResponse>? WithholdingTaxDataCollectionResponse { get; set; }
    }
}
