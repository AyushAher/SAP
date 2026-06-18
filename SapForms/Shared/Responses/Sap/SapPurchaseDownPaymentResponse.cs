namespace Shared.Responses.Sap
{
    public record SapPurchaseDownPaymentResponse : SapBaseResponse
    {
        public int? DocEntry { get; set; }
        public int? DocNum { get; set; }
        public double? DownPaymentAmount { get; set; }
        public double? WTAmount { get; set; }
        public double? DownPaymentPercentage { get; set; }
    }

        public record GetAllSapPurchaseDownPaymentResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapPurchaseDownPaymentResponse>? Value { get; set; }
    }

}