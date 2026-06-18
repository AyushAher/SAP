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
}
