using FluentAssertions;
using SapApi.Infrastructure.Persistence;

namespace SapApi.Tests.Persistence;

public class DateTimeUtcConverterTests
{
    [Test]
    public void ToUtc_LeavesUtcUnchanged()
    {
        var utc = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc);
        DateTimeUtcConverter.ToUtc(utc).Kind.Should().Be(DateTimeKind.Utc);
        DateTimeUtcConverter.ToUtc(utc).Should().Be(utc);
    }

    [Test]
    public void ToUtc_ConvertsLocalToUtc()
    {
        var local = new DateTime(2026, 6, 5, 15, 30, 0, DateTimeKind.Local);
        var result = DateTimeUtcConverter.ToUtc(local);
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(local.ToUniversalTime());
    }

    [Test]
    public void ToUtc_TagsUnspecifiedAsUtc()
    {
        var unspecified = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Unspecified);
        var result = DateTimeUtcConverter.ToUtc(unspecified);
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Ticks.Should().Be(unspecified.Ticks);
    }
}
