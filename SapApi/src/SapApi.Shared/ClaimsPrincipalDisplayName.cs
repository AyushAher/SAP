using System.Security.Claims;

namespace SapApi.Shared;

/// <summary>
/// Resolves a human-readable display name from JWT / identity claims.
/// Prefers FullName (UI header name) over login UserName; never uses numeric user id.
/// </summary>
public static class ClaimsPrincipalDisplayName
{
    public const string FullNameClaimType = "FullName";

    public static string GetDisplayName(ClaimsPrincipal? user)
    {
        if (user is null)
            return string.Empty;

        var fullName = user.FindFirst(FullNameClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName.Trim();

        var userName = user.Identity?.Name
            ?? user.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(userName))
            return userName.Trim();

        return string.Empty;
    }
}
