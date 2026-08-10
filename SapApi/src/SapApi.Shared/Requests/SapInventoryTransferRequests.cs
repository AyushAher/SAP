using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests
{
    public record SapInventoryTransferRequests : SapInventoryTransferRequestResponse;

    public record SapInventoryTransferItemsRequests
    {
        [JsonPropertyName("ItemCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemCode { get; set; }
        
        [JsonPropertyName("LineNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LineNum { get; set; }

        [JsonPropertyName("Quantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Quantity { get; set; }

        [JsonPropertyName("UnitPrice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? UnitPrice { get; set; }

        [JsonPropertyName("DiscountPercent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DiscountPercent { get; set; }

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

        /// <summary>SAP DocumentLines.FreeText — free-text remarks on the line.</summary>
        [JsonPropertyName("FreeText"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FreeText { get; set; }

        [JsonPropertyName("AccountCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AccountCode { get; set; }

        /// <summary>India GST — AbsEntry from India HSN (OCHP) master.</summary>
        [JsonPropertyName("HSNEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? HSNEntry { get; set; }

        /// <summary>India GST — AbsEntry from India SAC master (service items).</summary>
        [JsonPropertyName("SACEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SACEntry { get; set; }

        [JsonPropertyName("UoMCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UoMCode { get; set; }

        [JsonPropertyName("UoMEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UoMEntry { get; set; }

        /// <summary>SAP NumPerMsr — items per purchase unit (Inventory qty ÷ Purchase qty).</summary>
        [JsonPropertyName("UnitsOfMeasurment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? UnitsOfMeasurment { get; set; }

        /// <summary>Inventory/stock quantity for the line (Quantity × UnitsOfMeasurment).</summary>
        [JsonPropertyName("InventoryQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? InventoryQuantity { get; set; }

        /// <summary>
        /// SAP UseBaseUn — Inventory UoM Yes/No. tYES when UnitsOfMeasurment is 1, otherwise tNO.
        /// </summary>
        [JsonPropertyName("UseBaseUnits"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UseBaseUnits { get; set; }

        [JsonPropertyName("ProjectCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectCode { get; set; }

        [JsonPropertyName("CostingCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CostingCode { get; set; }

        [JsonPropertyName("CostingCode2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CostingCode2 { get; set; }

        [JsonPropertyName("CostingCode3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CostingCode3 { get; set; }

        [JsonPropertyName("CostingCode4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CostingCode4 { get; set; }

        [JsonPropertyName("CostingCode5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CostingCode5 { get; set; }

        /// <summary>Production order no. UDF — mandatory on lines when header U_PO_Type = JOB.</summary>
        [JsonPropertyName("U_ProdNo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UProdNo { get; set; }

        [JsonIgnore]
        public double RowTotalAfterDisc => (UnitPrice ?? 0) * (Quantity ?? 0) * (1 - (DiscountPercent ?? 0) / 100);
        [JsonIgnore]
        public double RowTaxAmount => RowTotalAfterDisc * (TaxPercentagePerRow ?? 0) / 100;
        [JsonIgnore]
        public double LineGrandTotal => RowTotalAfterDisc + RowTaxAmount;
        public string GetWTLiableValue() =>
            WTLiable == Constants.SapBoolean.SapTrue ? "Yes" : "No";
        public string GetTaxLiableValue() =>
            TaxLiable == Constants.SapBoolean.SapTrue ? "Yes" : "No";

    }
}