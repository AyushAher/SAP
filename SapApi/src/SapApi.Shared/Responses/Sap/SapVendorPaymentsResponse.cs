using SapApi.Shared.Requests;

namespace SapApi.Shared.Responses.Sap
{
    public record SapVendorPaymentsResponse : SapBaseResponse
    {
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNumber { get; set; }

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }

        [JsonPropertyName("U_EmpName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PoNumber { get; set; }

        [JsonPropertyName("Cancelled"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Cancelled { get; set; }

        [JsonPropertyName("PaymentInvoices"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<PaymentInvoice>? PaymentInvoices { get; set; }
    }

    public record GetAllSapVendorPaymentsResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapVendorPaymentsResponse>? Value { get; set; }
    }
}
