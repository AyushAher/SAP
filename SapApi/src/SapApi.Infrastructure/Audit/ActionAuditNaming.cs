namespace SapApi.Infrastructure.Audit;

public static class ActionAuditNaming
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    public static bool ShouldAudit(string httpMethod, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalizedPath = NormalizePath(path);
        if (IsIgnoredPath(normalizedPath))
            return false;

        if (!normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsAuthAction(normalizedPath, httpMethod))
            return true;

        if (!MutatingMethods.Contains(httpMethod))
            return false;

        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && IsReadOnlyPost(normalizedPath))
            return false;

        return true;
    }

    public static string BuildActionLabel(string httpMethod, string path)
    {
        var normalizedPath = NormalizePath(path);
        if (IsAuthAction(normalizedPath, httpMethod))
            return BuildAuthActionLabel(normalizedPath, httpMethod);

        var segments = normalizedPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return $"{httpMethod.ToUpperInvariant()} {normalizedPath}";

        var resourceSegment = segments[1];
        var resource = ToResourceName(resourceSegment);
        var verb = httpMethod.ToUpperInvariant() switch
        {
            "POST" => ResolvePostVerb(segments),
            "PUT" or "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => httpMethod.ToUpperInvariant(),
        };

        return $"{resource}.{verb}";
    }

    private static bool IsIgnoredPath(string path) =>
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/swagger", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/auth/public-key", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/auth/company-databases", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthAction(string path, string httpMethod) =>
        path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
        && MutatingMethods.Contains(httpMethod);

    private static bool IsReadOnlyPost(string path) =>
        path.EndsWith("/list", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/list/", StringComparison.OrdinalIgnoreCase);

    private static string BuildAuthActionLabel(string path, string httpMethod)
    {
        var action = path["/api/auth/".Length..];
        var name = action switch
        {
            "login" => "Login",
            "register" => "Register",
            "refresh" => "RefreshToken",
            "logout" => "Logout",
            "switch-company" => "SwitchCompany",
            "switch-branch" => "SwitchBranch",
            _ => ToResourceName(action),
        };
        return $"Auth.{name}";
    }

    private static string ResolvePostVerb(string[] segments)
    {
        if (segments.Length == 2)
            return "Create";

        var tail = segments[^1];
        if (tail.Equals("approve", StringComparison.OrdinalIgnoreCase))
            return "Approve";
        if (tail.Equals("reject", StringComparison.OrdinalIgnoreCase))
            return "Reject";
        if (tail.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            return "Cancel";
        if (tail.Equals("retry", StringComparison.OrdinalIgnoreCase))
            return "Retry";
        if (tail.Equals("sync", StringComparison.OrdinalIgnoreCase))
            return "Sync";
        if (tail.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return "DownloadPdf";

        return "Create";
    }

    private static string ToResourceName(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "Unknown";

        var parts = segment.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = string.Concat(parts.Select(static part =>
            part.Length == 0 ? string.Empty : char.ToUpperInvariant(part[0]) + part[1..]));

        if (name.EndsWith('s') && name.Length > 1)
            name = name[..^1];

        return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        var queryIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            trimmed = trimmed[..queryIndex];

        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
