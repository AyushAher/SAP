namespace SapApi.Domain.Entities;

/// <summary>
/// Local mirror of a SAP Business One Item master (OITM) row. SAP remains the write authority;
/// this table serves item search/lookup reads after sync so the UI never blocks on a live SAP
/// round-trip for every keystroke.
/// </summary>
public class Item : ISoftDeletable
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public string CompanyDb { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int? ItemGroupCode { get; set; }
    public int? ItemsGroupCode { get; set; }
    public string? InventoryItem { get; set; }
    public string? InventoryUom { get; set; }
    public string? PurchaseUnit { get; set; }
    public double? PurchaseItemsPerUnit { get; set; }
    public double? InventoryWeight { get; set; }
    public int? UoMGroupEntry { get; set; }
    public int? InventoryUoMEntry { get; set; }
    public int? DefaultPurchasingUoMEntry { get; set; }
    public string? ChapterID { get; set; }
    public string? DefaultWarehouse { get; set; }
    public string? GstRelevant { get; set; }
    public string? PurchaseVatGroup { get; set; }

    public DateTime SyncedAtUtc { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }
}

/// <summary>Per-company sync metadata for the item master catalog (including Hangfire full-sync job).</summary>
public class ItemSyncState : ISoftDeletable
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
    /// <summary>Highest ItemCode processed by the current/last full sync job (string cursor — items
    /// have no monotonic numeric key like DocEntry).</summary>
    public string? LastItemCode { get; set; }
}
