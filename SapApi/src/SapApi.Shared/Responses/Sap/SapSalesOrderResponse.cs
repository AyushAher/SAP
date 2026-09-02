namespace SapApi.Shared.Responses.Sap
{
    public record SapSalesOrderResponse : SapBaseResponse
    {
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocumentNumber { get; set; }

        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }

        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardName { get; set; }

        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("NumAtCard"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NumAtCard { get; set; }

        [JsonPropertyName("Project"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Project { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapSalesOrderDocumentLinesResponse>? DocumentLines { get; set; }
    }

    public record GetAllSapSalesOrderResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapSalesOrderResponse>? Value { get; set; }
    }

    public record SapSalesOrderDocumentLinesResponse
    {
        [JsonPropertyName("LineNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LineNum { get; set; }

        [JsonPropertyName("ItemCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemCode { get; set; }

        [JsonPropertyName("ItemDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemName { get; set; }

        [JsonPropertyName("Quantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Quantity { get; set; }

        /// <summary>SAP NumPerMsr — inventory units per sales unit on the row.</summary>
        [JsonPropertyName("UnitsOfMeasurment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? UnitsOfMeasurment { get; set; }

        [JsonPropertyName("InventoryQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? InventoryQuantity { get; set; }
    }
}
