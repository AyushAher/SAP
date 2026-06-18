namespace Shared.Responses.Sap
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
        [JsonPropertyName("ItemCode")] public string ItemCode { get; set; }
        [JsonPropertyName("ItemDescription")] public string ItemName { get; set; }
    }
}
