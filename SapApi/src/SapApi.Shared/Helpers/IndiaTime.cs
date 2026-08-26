using System.Globalization;

namespace SapApi.Shared.Helpers;

/// <summary>
/// India Standard Time (IST, UTC+05:30 / Asia/Kolkata). Docker containers run UTC, so
/// printed timestamps must convert explicitly rather than using <see cref="DateTime.Now"/>.
/// </summary>
public static class IndiaTime
{
    public static TimeZoneInfo TimeZone { get; } = Resolve();

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone);

    public static string FormatPrintedOn(DateTimeOffset? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow ?? DateTimeOffset.UtcNow, TimeZone);
        return local.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " IST";
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next id (Windows vs IANA).
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "IST",
            TimeSpan.FromHours(5.5),
            "India Standard Time",
            "India Standard Time");
    }
}
