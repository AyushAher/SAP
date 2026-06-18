namespace Shared.Responses.Sap
{
    public record SapPurchaseInvoicesResponse : SapBaseResponse
    {
        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocEntry { get; set; }
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocNum { get; set; }
        
        [JsonPropertyName("WTAmount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? WTAmount { get; set; }

        
        [JsonPropertyName("WithholdingTaxDataCollection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<WithholdingTaxDataCollectionResponse>? WithholdingTaxDataCollection { get; set; }

        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DocTotal { get; set; }

        [JsonPropertyName("PaidToDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PaidToDate { get; set; }

        [JsonPropertyName("DocumentStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocumentStatus { get; set; }

        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CardCode { get; set; }

        [JsonPropertyName("NumAtCard"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NumAtCard { get; set; }

        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapPurchaseInvoiceDocumentLines>? DocumentLines { get; set; }
        public int? BaseType => DocumentLines?.FirstOrDefault()?.BaseType;
        public int? BaseEntry => DocumentLines?.FirstOrDefault()?.BaseEntry;
    }

    public record SapPurchaseInvoiceDocumentLines
    {
        public int? LineNum { get; set; }
        public int? BaseType { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLine { get; set; }
    }
    public record GetAllSapPurchaseInvoicesResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapPurchaseInvoicesResponse>? Value { get; set; } = [];

    }

}