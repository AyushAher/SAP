namespace SapApi.PoSync;

/// <summary>
/// Console sync settings. Bind from the <c>PurchaseOrderSync</c> section in appsettings.
/// CLI flags override these when provided.
/// </summary>
public class PurchaseOrderSyncOptions
{
    public const string Label = "PurchaseOrderSync";

    /// <summary>
    /// <c>new</c> — incremental (DocEntries above local max),
    /// <c>full</c> — re-sync every PO from SAP,
    /// <c>one</c> — single DocEntry (requires <see cref="DocEntry"/>).
    /// </summary>
    public string Mode { get; set; } = "new";

    /// <summary>
    /// Optional company DB filter (e.g. PBBPL_UAT). When empty, every
    /// <c>SapCredentials:Accounts</c> entry is synced.
    /// </summary>
    public string? CompanyDb { get; set; }

    /// <summary>Resume cursor for batched new/full sync.</summary>
    public int? AfterDocEntry { get; set; }

    /// <summary>Required when <see cref="Mode"/> is <c>one</c>.</summary>
    public int? DocEntry { get; set; }

    /// <summary>
    /// Synthetic app user id used as the SAP session cache key (same idea as Hangfire ServiceUserId).
    /// </summary>
    public int ServiceUserId { get; set; }

    /// <summary>When true, applies EF migrations before syncing.</summary>
    public bool MigrateDatabase { get; set; }

    /// <summary>
    /// Keep calling the sync batch until SAP reports no more work.
    /// The API caps each batch (~400 docs / ~25s) for reverse-proxy timeouts; the console loops.
    /// </summary>
    public bool ContinueUntilComplete { get; set; } = true;
}
