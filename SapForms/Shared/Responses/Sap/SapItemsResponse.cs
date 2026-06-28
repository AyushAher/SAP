namespace Shared.Responses.Sap
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
        [JsonPropertyName("InventoryItem"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InventoryItem { get; set; }
        [JsonPropertyName("InventoryUOM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? InventoryUom { get; set; }
        [JsonPropertyName("InventoryWeight"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? InventoryWeight { get; set; }
    }
}