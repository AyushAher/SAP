namespace SapApi.Shared.Responses.Sap
{
    public record SapWarehousesResponse : SapBaseResponse
    {
        [JsonPropertyName("value")] public List<WarehouseResponse>? Value { get; set; }

    }
    public record WarehouseResponse
    {
        [JsonPropertyName("WarehouseCode")] public string? WarehouseCode { get; set; }
        [JsonPropertyName("WarehouseName")] public string? WarehouseName { get; set; }
        [JsonPropertyName("State")] public string? State { get; set; }
        [JsonPropertyName("Location")] public int? Location { get; set; }
        [JsonPropertyName("City")] public string? City { get; set; }
        [JsonPropertyName("Street")] public string? Street { get; set; }
        [JsonPropertyName("StreetNo")] public string? StreetNo { get; set; }
        [JsonPropertyName("Block")] public string? Block { get; set; }
        [JsonPropertyName("BuildingFloorRoom")] public string? BuildingFloorRoom { get; set; }
        [JsonPropertyName("ZipCode")] public string? ZipCode { get; set; }
        [JsonPropertyName("Country")] public string? Country { get; set; }
        [JsonPropertyName("FederalTaxID")] public string? FederalTaxID { get; set; }
    }
}