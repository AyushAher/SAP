namespace Shared.Responses.Sap
{
    public record SapVendorPaymentsResponse : SapBaseResponse
    {
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNumber { get; set; }

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }

        [JsonPropertyName("U_ReqId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApprovalRequestId { get; set; }
        
    }

    public record GetAllSapVendorPaymentsResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapVendorPaymentsResponse>? Value { get; set; }
    }
}
