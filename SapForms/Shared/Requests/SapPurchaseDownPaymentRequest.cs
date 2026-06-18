using Shared.Responses.Sap;

namespace Shared.Requests
{
    public class SapPurchaseDownPaymentRequest
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }
        [JsonPropertyName("Comments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Comments { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryTransferItemsRequests>? DocumentLines { get; set; } = [];

        [JsonPropertyName("WithholdingTaxDataCollection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapWithholdingTaxDataCollectionResponse>? WithholdingTaxDataCollection { get; set; } = [];

        [JsonPropertyName("DownPaymentType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DownPaymentType { get; set; } = "dptRequest";
        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JournalMemo { get; set; }

        [JsonPropertyName("DownPayment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DownPayment { get; set; }
        [JsonPropertyName("DocType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocType { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }

        [JsonPropertyName("DocDueDate")]
        public DateTime DocDueDate { get; set; } = DateTime.Now;
        [JsonPropertyName("BPL_IDAssignedToInvoice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BPLId { get; set; }

        [JsonPropertyName("U_FIST"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApprovalRequestId { get; set; }
    }
}