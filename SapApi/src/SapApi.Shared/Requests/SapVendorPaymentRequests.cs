namespace SapApi.Shared.Requests
{
    public class SapVendorPaymentRequests
    {
        [JsonPropertyName("CardCode")]
        public string CardCode { get; set; }

        [JsonPropertyName("TransferSum")]
        public string TransferSum { get; set; } = "0";
        
        [JsonPropertyName("ProjectCode")]
        public string? ProjectCode { get; set; }
        
        [JsonPropertyName("U_EmpName")]
        public string? PoNumber { get; set; }

        [JsonPropertyName("TransferReference")]
        public string TransferReference { get; set; }

        [JsonPropertyName("CounterReference")]
        public string CounterReference { get; set; }

        [JsonPropertyName("TransferDate")]
        public DateTime TransferDate { get; set; }

        [JsonPropertyName("TransferAccount")]
        public string TransferAccount { get; set; } = "0";

        [JsonPropertyName("JournalRemarks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JournalRemarks { get; set; }
        [JsonPropertyName("Remarks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Remarks { get; set; }

        [JsonPropertyName("CashFlowAssignments")]
        public List<CashFlowAssignments> CashFlowAssignments { get; set; } = [];

        [JsonPropertyName("PaymentInvoices")]
        public List<PaymentInvoice> PaymentInvoices { get; set; } = [];

        [JsonPropertyName("DueDate")]
        public DateTime DocDueDate { get; set; } = DateTime.Now;

        [JsonPropertyName("DocDate")]
        public DateTime DocDate { get; set; } = DateTime.Now;

        [JsonPropertyName("TaxDate")]
        public DateTime PostingDate { get; set; } = DateTime.Now;

        [JsonPropertyName("BPLID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BPLId { get; set; }
    }

    public class CashFlowAssignments
    {
        [JsonPropertyName("AmountLC")]
        public string AmountLc { get; set; } = "0";

        [JsonPropertyName("PaymentMeans")]
        public string PaymentMeans { get; set; } = Constants.SapPaymentMeansType.BankTransfer;

        [JsonPropertyName("CashFlowLineItemID")]
        public int CashFlowLineItemID { get; set; }
    }

    public class PaymentInvoice
    {
        [JsonPropertyName("LineNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LineNumber { get; set; }

        [JsonPropertyName("InvoiceType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InvoiceType { get; set; } = Constants.SapVendorPaymentInvoiceType.Invoice;

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }

        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNum { get; set; }

        [JsonPropertyName("SumApplied")]
        public double SumApplied { get; set; }

        [JsonPropertyName("AppliedFC")]
        public double AppliedFC { get; set; }

    }
}
