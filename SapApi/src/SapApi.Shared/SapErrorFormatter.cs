using System.Net;
using System.Text.Json;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared;

/// <summary>
/// Extracts a user-facing message from SAP Service Layer error JSON.
/// </summary>
public static class SapErrorFormatter
{
    public static string Format(SapBaseResponse? sapResult, string? rawJson, HttpStatusCode statusCode)
    {
        var fromObject = sapResult?.Error?.Message?.Value;
        if (!string.IsNullOrWhiteSpace(fromObject))
            return Clarify(fromObject.Trim(), sapResult?.Error?.Code);

        var fromJson = TryExtractMessage(rawJson);
        if (!string.IsNullOrWhiteSpace(fromJson))
            return Clarify(fromJson.Trim(), sapResult?.Error?.Code);

        return $"SAP Service Layer request failed ({(int)statusCode}).";
    }

    /// <summary>
    /// Maps opaque SAP ODBC codes / series messages to actionable guidance without hiding the original text.
    /// </summary>
    internal static string Clarify(string message, int? sapErrorCode)
    {
        var isNumberingSeriesRequired =
            message.Contains("define the numbering series", StringComparison.OrdinalIgnoreCase)
            || message.Contains("first define the numbering series", StringComparison.OrdinalIgnoreCase)
            || (sapErrorCode == 131 && message.Contains("series", StringComparison.OrdinalIgnoreCase))
            || message.Contains("131-3", StringComparison.OrdinalIgnoreCase);

        if (isNumberingSeriesRequired)
        {
            return $"{message} — the SAP user logged into Service Layer has no valid default numbering series "
                + "for this document (or the default is locked / wrong financial year / wrong branch). "
                + "The app should set Series on the payload; if this still appears, ask a SAP admin: "
                + "Administration → System Initialization → Document Numbering → select the document "
                + "(e.g. A/P Down Payment) → set the user's default series for the current FY and branch.";
        }

        var is2028 = sapErrorCode == -2028
            || message.Contains("ODBC -2028", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No matching records found", StringComparison.OrdinalIgnoreCase);

        if (!is2028)
            return message;

        return $"{message} — often a missing document numbering series for the selected branch and posting-date financial year, "
            + "or a missing master (BP, project, warehouse, employee). "
            + "Ask a SAP admin to verify document series under Document Numbering for that branch/year.";
    }

    public static string? TryExtractMessage(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                if (message.ValueKind == JsonValueKind.Object
                    && message.TryGetProperty("value", out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall through — caller may use the raw body.
        }

        return rawJson.Length <= 500 ? rawJson : rawJson[..500];
    }
}
