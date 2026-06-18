namespace SapApi.Modals.Responses.Sap
{
    public class SapPurchaseDownPaymentResponse
    {
        public int? DocEntry { get; set; }
        public int? DocNum { get; set; }
        public double? DownPaymentAmount { get; set; }
        public double? DownPaymentPercentage { get; set; }
    }
}