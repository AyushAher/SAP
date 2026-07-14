using System.Text.Json;
using System.Text.Json.Serialization;
using SapApi.Infrastructure.Persistence;

/// <summary>
/// Ensures DateTime values from JSON (often Kind=Unspecified) are tagged UTC before domain/EF use.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return DateTimeUtcConverter.ToUtc(value);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTimeUtcConverter.ToUtc(value));
}

public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetDateTime();
        return DateTimeUtcConverter.ToUtc(value);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(DateTimeUtcConverter.ToUtc(value.Value));
    }
}
