using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests
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

        /// <summary>
        /// SAP Service Layer maps <c>DownPayment</c> to ODPO.DpmPrcnt (percentage).
        /// Do not send the payment amount here — use <see cref="DownPaymentAmount"/> instead.
        /// </summary>
        [JsonPropertyName("DownPayment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DownPayment { get; set; }

        /// <summary>SAP Service Layer ODPO.DpmAmnt — down payment amount in document currency.</summary>
        [JsonPropertyName("DownPaymentAmount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DownPaymentAmount { get; set; }

        [JsonPropertyName("DocType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocType { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }

        /// <summary>SAP Service Layer posting date (ODPO.DocDate).</summary>
        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DocDate { get; set; }

        /// <summary>SAP Service Layer document/tax date (ODPO.TaxDate). Kept in sync with <see cref="DocDate"/>.</summary>
        [JsonPropertyName("TaxDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? TaxDate { get; set; }

        [JsonPropertyName("DocDueDate")]
        public DateTime DocDueDate { get; set; } = DateTime.Now;
        [JsonPropertyName("BPL_IDAssignedToInvoice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BPLId { get; set; }

        /// <summary>Payment Request ID (<see cref="Domain.Entities.StageWisePayment.Id"/>) sent to SAP UDF U_BSC_3.</summary>
        [JsonPropertyName("U_BSC_3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PaymentRequestId { get; set; }
    }
}