using System.Text.Json.Serialization;

namespace Shared.Responses.Sap
{
    public record SapWarehousesResponse : SapBaseResponse
    {
        [JsonPropertyName("value")] public List<WarehouseResponse>? Value { get; set; }

    }
    public record WarehouseResponse
    {
        [JsonPropertyName("WarehouseCode")] public string? WarehouseCode { get; set; }
        [JsonPropertyName("State")] public string? State { get; set; }
        [JsonPropertyName("Location")] public int? Location { get; set; }
        [JsonPropertyName("City")] public string? City { get; set; }
    }
}