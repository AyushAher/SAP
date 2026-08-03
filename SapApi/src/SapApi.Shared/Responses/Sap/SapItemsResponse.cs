using SapApi.Shared.Serialization;

namespace SapApi.Shared.Responses.Sap
{
    public record SapItemsResponse : SapBaseResponse
    {
        [JsonPropertyName("value")] public List<ItemsResponse>? Value { get; set; }
    }

    public record ItemsResponse
    {
        [JsonPropertyName("ItemCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ItemCode { get; set; }
        [JsonPropertyName("ItemName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ItemName { get; set; }
        [JsonPropertyName("ItemGroupCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ItemGroupCode { get; set; }
        [JsonPropertyName("ItemsGroupCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ItemsGroupCode { get; set; }
        [JsonPropertyName("InventoryItem"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InventoryItem { get; set; }
        [JsonPropertyName("InventoryUOM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InventoryUom { get; set; }
        [JsonPropertyName("PurchaseUnit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PurchaseUnit { get; set; }
        /// <summary>Items per purchase unit (NumInBuy) — used as default UnitsOfMeasurment on PO lines.</summary>
        [JsonPropertyName("PurchaseItemsPerUnit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? PurchaseItemsPerUnit { get; set; }
        [JsonPropertyName("InventoryWeight"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? InventoryWeight { get; set; }
        /// <summary>
        /// India GST — on Items this is typically the HSN AbsEntry (number in SL JSON).
        /// Stored as string so UI can resolve label via IndiaHsnService.
        /// </summary>
        [JsonPropertyName("ChapterID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string? ChapterID { get; set; }
        [JsonPropertyName("DefaultWarehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DefaultWarehouse { get; set; }
        [JsonPropertyName("GSTRelevnt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? GstRelevant { get; set; }
        [JsonPropertyName("PurchaseVATGroup"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PurchaseVatGroup { get; set; }
    }
}