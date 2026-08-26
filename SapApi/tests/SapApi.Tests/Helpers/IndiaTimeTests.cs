using FluentAssertions;
using SapApi.Shared.Helpers;

namespace SapApi.Tests.Helpers;

[TestFixture]
public class IndiaTimeTests
{
    [Test]
    public void FormatPrintedOn_converts_utc_to_ist()
    {
        var utc = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        IndiaTime.FormatPrintedOn(utc).Should().Be("26/08/2026 17:30 IST");
    }

    [Test]
    public void TimeZone_is_utc_plus_five_and_a_half_hours()
    {
        IndiaTime.TimeZone.BaseUtcOffset.Should().Be(TimeSpan.FromHours(5.5));
    }
}
