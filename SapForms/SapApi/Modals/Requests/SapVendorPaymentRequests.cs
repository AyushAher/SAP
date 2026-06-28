namespace SapApi.Modals.Requests
{
    public class SapVendorPaymentRequests
    {
        [JsonPropertyName("CardCode")]
        public string CardCode { get; set; }

        [JsonPropertyName("TransferSum")]
        public string TransferSum { get; set; } = "0";

        [JsonPropertyName("TransferDate")]
        public DateTime TransferDate { get; set; }

        [JsonPropertyName("TransferAccount")]
        public string TransferAccount { get; set; } = "0";

        [JsonPropertyName("CashFlowAssignments")]
        public List<CashFlowAssignments> CashFlowAssignments { get; set; } = [];
        
        [JsonPropertyName("PaymentInvoices")]
        public List<PaymentInvoice> PaymentInvoices { get; set; } = [];
    }

    public class CashFlowAssignments
    {
        [JsonPropertyName("AmountLC")]
        public string AmountLc { get; set; } = "0";

        [JsonPropertyName("PaymentMeans")]
        public string PaymentMeans { get; set; } = Constants.SapPaymentMeansType.BankTransfer;
    }

    public class PaymentInvoice
    {
        [JsonPropertyName("LineNum")]
        public int LineNumber { get; set; }

        [JsonPropertyName("DocEntry")]
        public int DocEntry { get; set; }

        [JsonPropertyName("DocNum")]
        public int DocNum { get; set; }

        [JsonPropertyName("SumApplied")]
        public double SumApplied { get; set; }

        [JsonPropertyName("AppliedFC")]
        public double AppliedFC { get; set; }

    }
}