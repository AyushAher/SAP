using System.Globalization;
using System.Text.Json;

namespace SapApi.Shared.Sap;

/// <summary>
/// SAP ProductionOrderLine.UoMCode must be a whole number (UoM entry), not an inventory UoM name like "KG".
/// </summary>
public static class SapProductionOrderUoMNormalizer
{
    /// <summary>
    /// Returns a whole-number UoM code when <paramref name="value"/> is numeric; otherwise null
    /// (so the property is omitted and SAP can default from the item).
    /// </summary>
    public static object? NormalizeUoMCode(object? value)
    {
        if (value is null) return null;

        switch (value)
        {
            case int i:
                return i;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                return (int)l;
            case short s:
                return (int)s;
            case byte b:
                return (int)b;
            case double d when double.IsFinite(d) && Math.Abs(d - Math.Truncate(d)) < double.Epsilon:
                return (int)d;
            case float f when float.IsFinite(f) && Math.Abs(f - Math.Truncate(f)) < float.Epsilon:
                return (int)f;
            case decimal m when m == decimal.Truncate(m) && m >= int.MinValue && m <= int.MaxValue:
                return (int)m;
            case string s:
                return TryParseWholeNumber(s);
            case JsonElement je:
                return NormalizeJsonElement(je);
            default:
                return TryParseWholeNumber(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }

    static object? NormalizeJsonElement(JsonElement je) =>
        je.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when je.TryGetInt32(out var n) => n,
            JsonValueKind.Number when je.TryGetDouble(out var d)
                && double.IsFinite(d)
                && Math.Abs(d - Math.Truncate(d)) < double.Epsilon
                && d is >= int.MinValue and <= int.MaxValue => (int)d,
            JsonValueKind.String => TryParseWholeNumber(je.GetString()),
            _ => null,
        };

    static object? TryParseWholeNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
