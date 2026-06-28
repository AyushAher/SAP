namespace Shared.Responses.Sap
{
    public record SapBusinessPartnersGetResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapBusinessPartners>? Value { get; set; }
    }
    public record SapBusinessPartners
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardCode { get; set; } //  : "S1001",
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardName { get; set; } //  : "AGARWAL INFRASTEEL PVT LTD",
        [JsonPropertyName("CardType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardType { get; set; } //  : "cSupplier",
        [JsonPropertyName("GroupCode")] public int GroupCode { get; set; } // : 101,
        [JsonPropertyName("Series")] public int Series { get; set; } // : 2,
    }
}