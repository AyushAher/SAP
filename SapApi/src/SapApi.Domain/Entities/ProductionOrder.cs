namespace SapApi.Domain.Entities;

/// <summary>
/// Local mirror of a SAP Business One Production Order (OWOR) header.
/// SAP remains the write authority; this table serves portal reads after sync.
/// </summary>
public class ProductionOrder : ISoftDeletable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public string CompanyDb { get; set; } = string.Empty;

    /// <summary>SAP key (ProductionOrders.AbsoluteEntry).</summary>
    public int AbsoluteEntry { get; set; }
    public int? DocumentNumber { get; set; }
    public int? Series { get; set; }

    public string? ItemNo { get; set; }
    public string? ProductDescription { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }

    /// <summary>U_ProdType.</summary>
    public string? ProductionCategory { get; set; }
    /// <summary>U_DwgNo.</summary>
    public string? DrawingNo { get; set; }

    public double? PlannedQuantity { get; set; }
    public double? CompletedQuantity { get; set; }
    public double? RejectedQuantity { get; set; }

    public string? Warehouse { get; set; }
    public string? InventoryUom { get; set; }
    public int? UoMEntry { get; set; }

    public string? CustomerCode { get; set; }
    /// <summary>
    /// Resolved business partner name. ProductionOrders has no customer-name field in SAP, so the
    /// sync resolves it from master data and stores it here — list filtering must not need a live lookup.
    /// </summary>
    public string? CustomerName { get; set; }

    public string? Project { get; set; }
    /// <summary>U_PrjName when SAP has it, otherwise the resolved project master name.</summary>
    public string? ProjectName { get; set; }

    /// <summary>ProductionOrderOriginEntry — DocEntry of the originating document (usually a sales order).</summary>
    public int? SalesOrderDocEntry { get; set; }
    /// <summary>ProductionOrderOriginNumber — the document number the list and pickers search on.</summary>
    public int? SalesOrderDocNum { get; set; }
    /// <summary>How the order was raised (for example bopooManual, bopooSalesOrder).</summary>
    public string? ProductionOrderOrigin { get; set; }

    public DateTime? PostingDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? ClosingDate { get; set; }
    public DateTime? CreationDate { get; set; }

    public string? Remarks { get; set; }
    public string? JournalRemarks { get; set; }
    public string? PickRemarks { get; set; }
    public string? Printed { get; set; }
    public int? Priority { get; set; }
    public int? UserSignature { get; set; }
    public int? TransactionNumber { get; set; }
    public int? AttachmentEntry { get; set; }
    public string? RoutingDateCalculation { get; set; }
    public string? UpdateAllocation { get; set; }

    public string? DistributionRule { get; set; }
    public string? DistributionRule2 { get; set; }
    public string? DistributionRule3 { get; set; }
    public string? DistributionRule4 { get; set; }
    public string? DistributionRule5 { get; set; }

    public DateTime SyncedAtUtc { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }

    public ICollection<ProductionOrderLine> Lines { get; set; } = [];
}

/// <summary>Local mirror of a SAP production order component line (WOR1).</summary>
public class ProductionOrderLine : ISoftDeletable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public int ProductionOrderId { get; set; }
    public int LineNumber { get; set; }

    public string? ItemNo { get; set; }
    public string? ItemName { get; set; }
    public string? ItemType { get; set; }
    public string? LineText { get; set; }

    public double? BaseQuantity { get; set; }
    public double? PlannedQuantity { get; set; }
    public double? IssuedQuantity { get; set; }
    public double? AdditionalQuantity { get; set; }

    public string? ProductionOrderIssueType { get; set; }
    public string? Warehouse { get; set; }
    public int? VisualOrder { get; set; }
    public int? LocationCode { get; set; }
    public string? Project { get; set; }

    public int? UoMEntry { get; set; }
    /// <summary>
    /// SAP returns a numeric UoM entry here (never a name such as "KG"), and rejects a PUT that
    /// sends anything else. Mirrored as a whole number so the value round-trips unchanged.
    /// </summary>
    public int? UoMCode { get; set; }

    public string? WipAccount { get; set; }
    public int? StageId { get; set; }
    public double? RequiredDays { get; set; }
    public string? ResourceAllocation { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string? DistributionRule { get; set; }
    public string? DistributionRule2 { get; set; }
    public string? DistributionRule3 { get; set; }
    public string? DistributionRule4 { get; set; }
    public string? DistributionRule5 { get; set; }

    /// <summary>U_FreeTxt.</summary>
    public string? FreeText { get; set; }
    /// <summary>U_DocNum.</summary>
    public string? DocNum { get; set; }

    public ProductionOrder ProductionOrder { get; set; } = null!;
}

/// <summary>Per-company sync metadata for production orders (including the Hangfire full-sync job).</summary>
public class ProductionOrderSyncState : ISoftDeletable
{
    public const string StatusIdle = "Idle";
    public const string StatusRunning = "Running";
    public const string StatusSucceeded = "Succeeded";
    public const string StatusFailed = "Failed";

    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public string CompanyDb { get; set; } = string.Empty;
    public DateTime? LastSyncedAtUtc { get; set; }
    public int? LastSyncedCount { get; set; }
    public string? LastSyncMessage { get; set; }

    /// <summary>Idle | Running | Succeeded | Failed</summary>
    public string Status { get; set; } = StatusIdle;
    public string? HangfireJobId { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    /// <summary>Highest AbsoluteEntry processed by the current/last full sync job.</summary>
    public int? LastAbsoluteEntry { get; set; }
}

/// <summary>
/// Append-only audit trail of production order sync activity, so users can see who refreshed
/// what and when without reading server logs.
/// </summary>
public class ProductionOrderSyncLog : ISoftDeletable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public string CompanyDb { get; set; } = string.Empty;

    /// <summary>one | new | full | gaps | open</summary>
    public string Mode { get; set; } = string.Empty;
    /// <summary>Set for row-level syncs; null for bulk runs.</summary>
    public int? AbsoluteEntry { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? CorrelationId { get; set; }

    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public long DurationMs { get; set; }
    public DateTime CreatedOn { get; set; }
}
