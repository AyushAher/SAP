namespace SapApi.Shared.Configuration;

/// <summary>
/// Hangfire background-job settings. Manager SAP credentials used by the master-data refresh job
/// come from <see cref="SapCredentials.Accounts"/> (one entry per company DB), not from this section.
/// </summary>
public class HangfireOptions
{
    public const string Label = "Hangfire";

    /// <summary>When false, Hangfire server + recurring jobs are not registered.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dashboard path (e.g. /hangfire). Empty disables the dashboard.</summary>
    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>
    /// Cron for master-data cache refresh. Default runs at :00 and :45 every hour so the 1-hour
    /// Redis cache is refreshed ~15 minutes before expiry.
    /// </summary>
    public string MasterDataRefreshCron { get; set; } = "0,45 * * * *";

    /// <summary>
    /// Synthetic app user id used as the SAP session key for background jobs. Does not need to
    /// exist in AspNetUsers — it only namespaces the distributed session cache entry.
    /// </summary>
    public int ServiceUserId { get; set; }
}
