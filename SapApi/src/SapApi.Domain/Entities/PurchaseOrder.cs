namespace SapApi.Domain.Entities;

/// <summary>
/// Local mirror of a SAP Business One Purchase Order (OPOR) header.
/// SAP remains the write authority; this table serves reads/reporting after sync.
/// </summary>
public class PurchaseOrder
{
    public int Id { get; set; }
    public string CompanyDb { get; set; } = string.Empty;

    public int DocEntry { get; set; }
    public int? DocNum { get; set; }
    public string? DocType { get; set; }
    public string? Project { get; set; }
    public string? CardCode { get; set; }
    public string? CardName { get; set; }
    public double? DocTotal { get; set; }
    public double? VatSum { get; set; }
    public string? NumAtCard { get; set; }
    public string? DocumentStatus { get; set; }
    public string? DocCurrency { get; set; }
    public double? DocRate { get; set; }
    public string? JournalMemo { get; set; }
    public string? Comments { get; set; }
    public int? SalesPersonCode { get; set; }
    public int? DocumentsOwner { get; set; }
    public int? TransportationCode { get; set; }
    public DateTime? DocDate { get; set; }
    public DateTime? DocDueDate { get; set; }
    public DateTime? TaxDate { get; set; }
    public int? BPLId { get; set; }
    public int? ContactPersonCode { get; set; }
    public string? ShipToCode { get; set; }
    public double? RoundingDiffAmount { get; set; }
    public double? TotalDiscount { get; set; }

    public string? UStage { get; set; }
    public string? UWarehouse { get; set; }
    public string? UOwner { get; set; }
    public string? UPoType { get; set; }
    public string? UTrn { get; set; }
    public string? UDisId { get; set; }
    public string? UDispachAdd { get; set; }
    public string? URemark { get; set; }
    public string? UDispatchTo { get; set; }
    public string? UContactPerson { get; set; }
    public string? UPriceBasis { get; set; }
    public string? UModeOfTransport { get; set; }
    public string? UMatOutDoc { get; set; }
    public string? UGoodsIssue { get; set; }
    public string? UMatInDoc { get; set; }
    public string? UGoodsReceipt { get; set; }
    public string? UDelTerms { get; set; }
    public string? UInspectionBy { get; set; }
    public string? UTransportation { get; set; }
    public string? USupervision { get; set; }
    public string? UTransitIns { get; set; }
    public string? UDrawDocs { get; set; }
    public string? ULoading { get; set; }
    public string? UWarranty { get; set; }
    public string? UUnloading { get; set; }
    public string? UOtherRemark { get; set; }
    public string? UPainting { get; set; }
    public string? UTestCerts { get; set; }

    public DateTime SyncedAtUtc { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
    public ICollection<PurchaseOrderPaymentTerm> PaymentTerms { get; set; } = [];
    public ICollection<StageWisePayment> StageWisePayments { get; set; } = [];
    public ICollection<StageWisePaymentBatch> StageWisePaymentBatches { get; set; } = [];
    public ICollection<ApprovalRequest> ApprovalRequests { get; set; } = [];
}

public class PurchaseOrderLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int LineNum { get; set; }

    public string? ItemCode { get; set; }
    public string? ItemDescription { get; set; }
    public string? AccountCode { get; set; }
    public double? Quantity { get; set; }
    public double? UnitPrice { get; set; }
    public double? DiscountPercent { get; set; }
    public double? LineTotal { get; set; }
    public double? TaxPercentagePerRow { get; set; }
    public double? TaxTotal { get; set; }
    public string? TaxCode { get; set; }
    public string? WTLiable { get; set; }
    public string? TaxLiable { get; set; }
    public double? GrossTotal { get; set; }
    public string? WarehouseCode { get; set; }
    public int? HSNEntry { get; set; }
    public int? SACEntry { get; set; }
    public string? UoMCode { get; set; }
    public int? UoMEntry { get; set; }
    public double? UnitsOfMeasurment { get; set; }
    public double? InventoryQuantity { get; set; }
    public string? UseBaseUnits { get; set; }
    public string? ProjectCode { get; set; }
    public string? CostingCode { get; set; }
    public string? CostingCode2 { get; set; }
    public string? CostingCode3 { get; set; }
    public string? CostingCode4 { get; set; }
    public string? CostingCode5 { get; set; }
    public string? UProdNo { get; set; }
    public int? BaseType { get; set; }
    public int? BaseEntry { get; set; }
    public int? BaseLine { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}

/// <summary>Normalized payment-term UDF slot (U_B/G/D/S/T 1–11).</summary>
public class PurchaseOrderPaymentTerm
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int Slot { get; set; }
    public int? Basic { get; set; }
    public int? Gst { get; set; }
    public string? Description { get; set; }
    public string? Stage { get; set; }
    public string? Type { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}

/// <summary>Per-company sync metadata for purchase orders (including Hangfire full-sync job).</summary>
public class PurchaseOrderSyncState
{
    public const string StatusIdle = "Idle";
    public const string StatusRunning = "Running";
    public const string StatusSucceeded = "Succeeded";
    public const string StatusFailed = "Failed";

    public int Id { get; set; }
    public string CompanyDb { get; set; } = string.Empty;
    public DateTime? LastSyncedAtUtc { get; set; }
    public int? LastSyncedCount { get; set; }
    public string? LastSyncMessage { get; set; }

    /// <summary>Idle | Running | Succeeded | Failed</summary>
    public string Status { get; set; } = StatusIdle;
    public string? HangfireJobId { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    /// <summary>Highest DocEntry processed by the current/last full sync job.</summary>
    public int? LastDocEntry { get; set; }
}
