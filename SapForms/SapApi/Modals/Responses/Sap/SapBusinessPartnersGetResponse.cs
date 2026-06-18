using System.Text.Json.Serialization;

namespace SapApi.Modals.Responses.Sap
{
    public record SapBusinessPartnersGetResponse : SapBaseResponse
    {
        [JsonPropertyName("value")] public List<SapBusinessPartners>? Value { get; set; }
    }
    public record SapBusinessPartners
    {
        [JsonPropertyName("CardCode")] public string? CardCode { get; set; } //  : "S1001",
        [JsonPropertyName("CardName")] public string? CardName { get; set; } //  : "AGARWAL INFRASTEEL PVT LTD",
        [JsonPropertyName("CardType")] public string? CardType { get; set; } //  : "cSupplier",
        [JsonPropertyName("GroupCode")] public int GroupCode { get; set; } // : 101,
        [JsonPropertyName("Series")] public int Series { get; set; } // : 2,
    }
}