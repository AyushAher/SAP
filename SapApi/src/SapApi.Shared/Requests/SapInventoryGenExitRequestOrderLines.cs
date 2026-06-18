using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests
{
    public class SapInventoryGenExitRequestOrderLines
    {
        public SapProductionOrdersResponse ProductionOrder { get; set; }
        public List<SapProductionOrderLines> ProductionOrderLinesEntryNumber { get; set; } = [];
    }

    public class SapInventoryGenExitRequestOrderRequest
    {
        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DocDate { get; set; }

        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DueDate { get; set; }

        [JsonPropertyName("DocumentLines")]
        public List<SapInventoryGenExitRequestOrderLinesRequest> DocumentLines { get; set; } = [];
    }

    public class SapInventoryGenExitRequestOrderLinesRequest
    {
        [JsonPropertyName("BaseEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BaseEntry { get; set; }

        [JsonPropertyName("CostingCode")]
        public string CostingCode { get; set; }

        [JsonPropertyName("Warehouse")]
        public string Warehouse { get; set; }

        [JsonPropertyName("BaseLine"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BaseLine { get; set; }

        [JsonPropertyName("Quantity")]
        public double Quantity { get; set; }


        [JsonPropertyName("ItemCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemCode { get; set; }
        [JsonPropertyName("ItemDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemDescription { get; set; }

        [JsonPropertyName("ShipDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? ShipDate { get; set; }
        [JsonPropertyName("Price"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Price { get; set; }
        [JsonPropertyName("LineTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? LineTotal { get; set; }

    }
}