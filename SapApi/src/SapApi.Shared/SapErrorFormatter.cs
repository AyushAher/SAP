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
            return fromObject.Trim();

        var fromJson = TryExtractMessage(rawJson);
        if (!string.IsNullOrWhiteSpace(fromJson))
            return fromJson.Trim();

        return $"SAP Service Layer request failed ({(int)statusCode}).";
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
