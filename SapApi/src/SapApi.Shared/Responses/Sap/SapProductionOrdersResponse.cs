namespace SapApi.Shared.Responses.Sap
{
    public record SapProductionOrdersResponse : SapBaseResponse
    {
        [JsonPropertyName("AbsoluteEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? AbsoluteEntry { get; set; }

        [JsonPropertyName("DocumentNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocumentNumber { get; set; }

        [JsonPropertyName("Series"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Series { get; set; }

        [JsonPropertyName("ItemNo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemNumber { get; set; }

        [JsonPropertyName("ProductionOrderStatus"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Status { get; set; }

        [JsonPropertyName("ProductionOrderType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        [JsonPropertyName("BPLID"), JsonIgnore]
        public int? BPLId { get; set; }

        [JsonPropertyName("U_ProdType")]
        public string ProductionCategory { get; set; } = "";

        [JsonPropertyName("PlannedQuantity")]
        public double PlannedQuantity { get; set; }

        [JsonPropertyName("CompletedQuantity")]
        public double CompletedQuantity { get; set; }

        [JsonPropertyName("U_CustomerName"), JsonIgnore]
        public string CustomerName { get; set; } = "";

        [JsonPropertyName("U_DwgNo")]
        public string DrawingNo { get; set; } = "";

        [JsonPropertyName("ProductionOrderOriginNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SalesOrderDocNum { get; set; }

        [JsonPropertyName("ProductionOrderOriginEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SalesOrderDocEntry { get; set; }

        [JsonPropertyName("ProductionOrderOrigin"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProductionOrderOrigin { get; set; }

        [JsonPropertyName("RejectedQuantity")]
        public double RejectedQuantity { get; set; }

        [JsonPropertyName("PostingDate")]
        public DateTime PostingDate { get; set; }

        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DueDate { get; set; }

        [JsonPropertyName("UserSignature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UserSignature { get; set; }

        [JsonPropertyName("Remarks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Remarks { get; set; }

        [JsonPropertyName("ClosingDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? ClosingDate { get; set; }

        [JsonPropertyName("ReleaseDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? ReleaseDate { get; set; }

        [JsonPropertyName("CustomerCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomerCode { get; set; }

        [JsonPropertyName("Warehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Warehouse { get; set; }

        [JsonIgnore]
        public string? IssWarehouse { get; set; }

        [JsonPropertyName("InventoryUOM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InventoryUom { get; set; }

        [JsonPropertyName("JournalRemarks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JournalRemarks { get; set; }

        [JsonPropertyName("TransactionNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TransactionNumber { get; set; }

        [JsonPropertyName("CreationDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? CreationDate { get; set; }

        [JsonPropertyName("Printed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Printed { get; set; }

        [JsonPropertyName("DistributionRule"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule { get; set; }

        [JsonPropertyName("Project"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Project { get; set; }

        [JsonPropertyName("DistributionRule2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule2 { get; set; }

        [JsonPropertyName("DistributionRule3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule3 { get; set; }

        [JsonPropertyName("DistributionRule4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule4 { get; set; }

        [JsonPropertyName("DistributionRule5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule5 { get; set; }

        [JsonPropertyName("UoMEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UoMEntry { get; set; }

        [JsonPropertyName("StartDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("ProductDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProductDescription { get; set; }

        [JsonPropertyName("Priority")]
        public int Priority { get; set; } = 100;

        [JsonPropertyName("RoutingDateCalculation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RoutingDateCalculation { get; set; }

        [JsonPropertyName("UpdateAllocation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UpdateAllocation { get; set; }

        [JsonPropertyName("SAPPassport"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SapPassport { get; set; }

        [JsonPropertyName("AttachmentEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? AttachmentEntry { get; set; }

        [JsonPropertyName("PickRemarks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PickRemarks { get; set; }

        [JsonPropertyName("U_PrjName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectName { get; set; }

        [JsonPropertyName("ProductionOrderLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrderLines>? ProductionOrderLines { get; set; } = [];

        [JsonPropertyName("ProductionOrdersSalesOrderLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrdersSalesOrderLine>? ProductionOrdersSalesOrderLines { get; set; } = [];

        [JsonPropertyName("ProductionOrdersStages"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrdersStage>? ProductionOrdersStages { get; set; } = [];

        [JsonPropertyName("ProductionOrdersDocumentReferences"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrdersDocumentReference>? ProductionOrdersDocumentReferences { get; set; } = [];
    }

    public record SapProductionOrderLines
    {
        [JsonPropertyName("DocumentAbsoluteEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DocumentAbsoluteEntry { get; set; }

        [JsonPropertyName("LineNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LineNumber { get; set; }

        [JsonPropertyName("ItemNo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemNo { get; set; }

        [JsonPropertyName("ItemName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ItemName { get; set; }

        [JsonPropertyName("BaseQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? BaseQuantity { get; set; }

        [JsonPropertyName("PlannedQuantity")]
        public double PlannedQuantity { get; set; }

        [JsonPropertyName("IssuedQuantity")]
        public double IssuedQuantity { get; set; }

        [JsonPropertyName("ProductionOrderIssueType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProductionOrderIssueType { get; set; }

        [JsonPropertyName("Warehouse"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Warehouse { get; set; }

        [JsonPropertyName("VisualOrder"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? VisualOrder { get; set; }

        [JsonPropertyName("DistributionRule"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule { get; set; }

        [JsonPropertyName("LocationCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LocationCode { get; set; }

        [JsonPropertyName("Project"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Project { get; set; }

        [JsonPropertyName("DistributionRule2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule2 { get; set; }

        [JsonPropertyName("DistributionRule3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule3 { get; set; }

        [JsonPropertyName("DistributionRule4"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule4 { get; set; }

        [JsonPropertyName("DistributionRule5"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DistributionRule5 { get; set; }

        [JsonPropertyName("UoMEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? UoMEntry { get; set; }

        [JsonPropertyName("UoMCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? UoMCode { get; set; }

        [JsonPropertyName("WipAccount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WipAccount { get; set; }

        [JsonPropertyName("ItemType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ItemType { get; set; }

        [JsonPropertyName("LineText"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LineText { get; set; }

        [JsonPropertyName("AdditionalQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? AdditionalQuantity { get; set; }

        [JsonPropertyName("ResourceAllocation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ResourceAllocation { get; set; }

        [JsonPropertyName("StartDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? StartDate { get; set; }

        [JsonPropertyName("EndDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? EndDate { get; set; }

        [JsonPropertyName("StageID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? StageId { get; set; }

        [JsonPropertyName("RequiredDays"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? RequiredDays { get; set; }

        [JsonPropertyName("WeightOfRecycledPlastic"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? WeightOfRecycledPlastic { get; set; }

        [JsonPropertyName("PlasticPackageExemptionReason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PlasticPackageExemptionReason { get; set; }

        [JsonPropertyName("U_FreeTxt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FreeText { get; set; }

        [JsonPropertyName("U_DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DocNum { get; set; }

        [JsonPropertyName("SerialNumbers"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrderLineSerialNumber>? SerialNumbers { get; set; } = [];

        [JsonPropertyName("BatchNumbers"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrderLineBatchNumber>? BatchNumbers { get; set; } = [];
    }

    public record SapProductionOrdersSalesOrderLine;

    public record SapProductionOrdersStage;

    public record SapProductionOrdersDocumentReference;

    public record SapProductionOrderLineSerialNumber;

    public record SapProductionOrderLineBatchNumber;

    public record GetAllSapProductionOrdersResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrdersResponse>? Value { get; set; }
    }

    public record GetAllSapProductionOrderLinesResponse : SapQueryBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProductionOrderLines>? Value { get; set; }
    }
}
