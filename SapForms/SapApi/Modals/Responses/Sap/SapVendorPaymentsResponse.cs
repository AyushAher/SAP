namespace SapApi.Modals.Responses.Sap
{
    public record SapVendorPaymentsResponse : SapBaseResponse
    {
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNumber { get; set; }

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }
    }
}
