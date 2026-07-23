namespace SapApi.Shared.Configuration;

/// <summary>
/// One SAP Service Layer login used for a specific company database (background jobs / CLI).
/// Bind via indexed env vars, e.g.:
///   SapCredentials__Accounts__0__Username
///   SapCredentials__Accounts__0__Password
///   SapCredentials__Accounts__0__CompanyDb
/// </summary>
public class SapCompanyCredential
{
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Must match a <see cref="Enums.SapCompanyDatabase"/> name (e.g. PBBPL_UAT).</summary>
    public string? CompanyDb { get; set; }
}

/// <summary>
/// Manager/service credentials keyed by company DB. Used by Hangfire master-data cache refresh
/// (one warm pass per account) and CLI tools. Never commit passwords — supply via env.
/// </summary>
public class SapCredentials
{
    public const string Label = "SapCredentials";

    /// <summary>One entry per company DB that background jobs should authenticate against.</summary>
    public List<SapCompanyCredential> Accounts { get; set; } = [];
}
