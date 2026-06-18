using System.Text.Json.Serialization;

namespace SapApi.Modals.Responses.Sap
{
    public record SapItemsResponse : SapBaseResponse
    {
        [JsonPropertyName("value")] public List<ItemsResponse>? Value { get; set; }
    }

    public record ItemsResponse
    {
        [JsonPropertyName("ItemCode")] public string? ItemCode { get; set; }
        [JsonPropertyName("ItemName")] public string? ItemName { get; set; }
        [JsonPropertyName("ItemGroupCode")] public int? ItemGroupCode { get; set; }
        [JsonPropertyName("InventoryItem")] public string? InventoryItem { get; set; }
    }
}