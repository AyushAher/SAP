using Shared;
using Shared.Responses.Sap;

namespace Shared.Requests
{
    public record SapInventoryTransferRequests : SapInventoryTransferRequestResponse;

    public record SapInventoryTransferItemsRequests
    {
        [JsonPropertyName("ItemCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemCode { get; set; }
        
        [JsonPropertyName("LineNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LineNum { get; set; }

        [JsonPropertyName("Quantity")] public double Quantity { get; set; }
        [JsonPropertyName("UnitPrice")] public double UnitPrice { get; set; }
        [JsonPropertyName("DiscountPercent")] public double DiscountPercent { get; set; }
        [JsonPropertyName("BaseType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? BaseType { get; set; }
        [JsonPropertyName("BaseEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BaseEntry { get; set; }
        [JsonPropertyName("BaseLine"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BaseLine { get; set; }

        [JsonPropertyName("LineTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? LineTotal { get; set; }

        [JsonPropertyName("TaxPercentagePerRow"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TaxPercentagePerRow { get; set; }

        [JsonPropertyName("TaxTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? TaxTotal { get; set; }

        [JsonPropertyName("TaxCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TaxCode { get; set; }

        [JsonPropertyName("WTLiable"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WTLiable { get; set; }

        [JsonPropertyName("TaxLiable"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TaxLiable { get; set; }

        [JsonPropertyName("GrossTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? GrossTotal { get; set; }

        [JsonPropertyName("WarehouseCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WarehouseCode { get; set; }

        [JsonPropertyName("FromWarehouseCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromWarehouseCode { get; set; }

        [JsonPropertyName("ItemDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemDescription { get; set; }
        [JsonPropertyName("AccountCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AccountCode { get; set; }
        
        [JsonIgnore]
        public double RowTotalAfterDisc => UnitPrice * Quantity;
        [JsonIgnore]
        public double RowTaxAmount => RowTotalAfterDisc * TaxPercentagePerRow ?? 0;
        [JsonIgnore]
        public double LineGrandTotal => RowTotalAfterDisc + RowTaxAmount;
        public string GetWTLiableValue() =>
            WTLiable == Constants.SapBoolean.SapTrue ? "Yes" : "No";
        public string GetTaxLiableValue() =>
            TaxLiable == Constants.SapBoolean.SapTrue ? "Yes" : "No";

    }
}