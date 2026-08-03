using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SapApi.Shared.Serialization;

/// <summary>
/// Reads JSON string or number into <see cref="string"/> (SAP often returns AbsEntry-like IDs as numbers).
/// </summary>
public sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var l) =>
                l.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for string."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
