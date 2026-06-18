using System.Text.Json.Serialization;

namespace Shared.Requests
{
    public record PurchaseOrderStageWisePaymentRequest
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryTransferItemsRequests>? DocumentLines { get; set; } = [];

        [JsonPropertyName("DownPaymentType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DownPaymentType { get; set; } = "dptInvoice";
    }
}