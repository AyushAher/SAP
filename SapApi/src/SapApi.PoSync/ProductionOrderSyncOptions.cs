namespace SapApi.PoSync;

/// <summary>
/// Console sync settings for production orders. Bind from the <c>ProductionOrderSync</c> section
/// in appsettings. CLI flags override these when provided.
/// </summary>
public class ProductionOrderSyncOptions
{
    public const string Label = "ProductionOrderSync";

    /// <summary>
    /// <c>new</c> — incremental (entries above the local max),
    /// <c>full</c> — re-sync every production order from SAP,
    /// <c>gaps</c> — pull entries missing between consecutive local AbsoluteEntries,
    /// <c>open</c> — re-read locally Planned/Released orders so status changes land,
    /// <c>one</c> — single AbsoluteEntry (requires <see cref="AbsoluteEntry"/>).
    /// </summary>
    public string Mode { get; set; } = "new";

    /// <summary>
    /// Optional company DB filter (e.g. PBBPL_UAT). When empty, every
    /// <c>SapCredentials:Accounts</c> entry is synced.
    /// </summary>
    public string? CompanyDb { get; set; }

    /// <summary>Resume cursor for batched new/full/open sync.</summary>
    public int? AfterAbsoluteEntry { get; set; }

    /// <summary>Required when <see cref="Mode"/> is <c>one</c>.</summary>
    public int? AbsoluteEntry { get; set; }

    /// <summary>
    /// Synthetic app user id used as the SAP session cache key (same idea as Hangfire ServiceUserId).
    /// </summary>
    public int ServiceUserId { get; set; }

    /// <summary>When true, applies EF migrations before syncing.</summary>
    public bool MigrateDatabase { get; set; }

    /// <summary>
    /// Keep calling the sync batch until SAP reports no more work. Each batch is capped
    /// (~400 docs / ~25s) so no single call can hit a reverse-proxy timeout; the console loops.
    /// </summary>
    public bool ContinueUntilComplete { get; set; } = true;
}
