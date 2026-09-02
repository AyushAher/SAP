using System.Globalization;
using System.Text.Json;

namespace SapApi.Shared.Sap;

/// <summary>
/// SAP ProductionOrderLine.UoMCode must be a whole number (UoM entry), not an inventory UoM name like "KG".
/// </summary>
public static class SapProductionOrderUoMNormalizer
{
    /// <summary>
    /// Returns a positive UoM entry when <paramref name="value"/> is a whole number greater than
    /// zero. Inventory names ("KG") and SAP's Manual-group placeholder (-1) are omitted so SAP
    /// can default from the item. Sending -1 as ProductionOrderLine.UoMCode fails the document
    /// with "Could not commit transaction: Error -1".
    /// </summary>
    public static object? NormalizeUoMCode(object? value)
    {
        if (value is null) return null;

        switch (value)
        {
            case int i:
                return PositiveOrNull(i);
            case long l when l is >= int.MinValue and <= int.MaxValue:
                return PositiveOrNull((int)l);
            case short s:
                return PositiveOrNull(s);
            case byte b:
                return PositiveOrNull(b);
            case double d when double.IsFinite(d) && Math.Abs(d - Math.Truncate(d)) < double.Epsilon:
                return PositiveOrNull((int)d);
            case float f when float.IsFinite(f) && Math.Abs(f - Math.Truncate(f)) < float.Epsilon:
                return PositiveOrNull((int)f);
            case decimal m when m == decimal.Truncate(m) && m >= int.MinValue && m <= int.MaxValue:
                return PositiveOrNull((int)m);
            case string s:
                return TryParseWholeNumber(s);
            case JsonElement je:
                return NormalizeJsonElement(je);
            default:
                return TryParseWholeNumber(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Header/line UoMEntry: keep only a real group entry, never Manual's -1.</summary>
    public static int? NormalizeUoMEntry(int? value) => value is > 0 ? value : null;

    static object? NormalizeJsonElement(JsonElement je) =>
        je.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when je.TryGetInt32(out var n) => PositiveOrNull(n),
            JsonValueKind.Number when je.TryGetDouble(out var d)
                && double.IsFinite(d)
                && Math.Abs(d - Math.Truncate(d)) < double.Epsilon
                && d is >= int.MinValue and <= int.MaxValue => PositiveOrNull((int)d),
            JsonValueKind.String => TryParseWholeNumber(je.GetString()),
            _ => null,
        };

    static object? TryParseWholeNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? PositiveOrNull(parsed)
            : null;
    }

    static object? PositiveOrNull(int value) => value > 0 ? value : null;
}
