namespace SapApi.Modals.Requests
{
    public class SapPurchaseDownPaymentRequest
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryTransferItemsRequests>? DocumentLines { get; set; } = [];

        [JsonPropertyName("DownPaymentType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DownPaymentType { get; set; } = "dptInvoice";

        [JsonPropertyName("DownPayment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DownPayment { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }
    }
}