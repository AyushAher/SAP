namespace Shared.Responses.Sap
{
    public record SapTaxCodesResponse : SapBaseResponse
    {
        [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
        [JsonPropertyName("Rate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Rate { get; set; }
        [JsonPropertyName("Code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; set; }
    }

    public record GetAllSapTaxCodesResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapTaxCodesResponse>? Value { get; set; }
    }
}
