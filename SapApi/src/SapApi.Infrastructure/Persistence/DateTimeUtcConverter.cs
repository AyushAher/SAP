using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SapApi.Infrastructure.Persistence;

public static class DateTimeUtcConverter
{
    public static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    public static DateTime? ToUtc(DateTime? value) =>
        value is null ? null : ToUtc(value.Value);

    /// <summary>
    /// Forces UTC Kind on write/read so Npgsql timestamptz accepts values from JSON (Unspecified).
    /// </summary>
    public static ValueConverter<DateTime, DateTime> Required { get; } = new(
        v => ToUtc(v),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static ValueConverter<DateTime?, DateTime?> Optional { get; } = new(
        v => v.HasValue ? ToUtc(v.Value) : null,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
}
