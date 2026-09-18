namespace SapApi.Shared.Configuration;

/// <summary>
/// Microsoft Graph app-only auth for sending mail as a shared mailbox (client-credentials flow —
/// no signed-in user). Replaces SMTP AUTH, which Microsoft 365 is phasing out tenant by tenant.
/// ClientSecret ships empty in appsettings.json on purpose; set it via the GRAPH_CLIENT_SECRET
/// environment variable at deploy time.
/// </summary>
public class GraphMailOptions
{
    public const string Label = "GraphMail";

    /// <summary>Entra ID (Azure AD) directory/tenant ID the app registration lives in.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) ID of the Entra ID app registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret for the app registration. Never committed — env var only.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The mailbox to send as (e.g. no-reply@PRIVILEGEBBPVTLTD.onmicrosoft.com), used both as the
    /// Graph API path (POST /users/{SenderMailbox}/sendMail) and the resulting From address. The
    /// app registration's Mail.Send permission must be granted (optionally scoped to just this
    /// mailbox via an application access policy) for this to work.
    /// </summary>
    public string SenderMailbox { get; set; } = string.Empty;
}
