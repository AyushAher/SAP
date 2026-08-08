using FluentAssertions;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapDocumentSeriesResolverTests
{
    [TestCase("2026-08-08", "FY26-27")]
    [TestCase("2026-04-01", "FY26-27")]
    [TestCase("2026-03-31", "FY25-26")]
    [TestCase("2025-04-01", "FY25-26")]
    public void GetIndiaFinancialYearPeriodIndicator_MatchesPbbplConvention(string isoDate, string expected)
    {
        var date = DateTime.Parse(isoDate);
        SapDocumentSeriesResolver.GetIndiaFinancialYearPeriodIndicator(date).Should().Be(expected);
    }

    [Test]
    public void FindSeries_MatchesBplAndPeriod_IgnoresLockedAndOtherBranches()
    {
        var series = new List<SapDocumentSeriesEntry>
        {
            new() { Series = 355, BPLID = 1, PeriodIndicator = "FY25-26", Locked = "tNO", Name = "PB25-26" },
            new() { Series = 589, BPLID = 1, PeriodIndicator = "FY26-27", Locked = "tNO", Name = "PB-26-27" },
            new() { Series = 999, BPLID = 1, PeriodIndicator = "FY26-27", Locked = "tYES", Name = "locked" },
            new() { Series = 615, BPLID = 5, PeriodIndicator = "FY26-27", Locked = "tNO", Name = "PE26-27" },
        };

        var match = SapDocumentSeriesResolver.FindSeries(series, bplId: 1, periodIndicator: "FY26-27");
        match.Should().NotBeNull();
        match!.Series.Should().Be(589);
        match.Name.Should().Be("PB-26-27");
    }

    [Test]
    public void FindSeries_ReturnsNull_WhenBranchMissingPeriodSeries()
    {
        var series = new List<SapDocumentSeriesEntry>
        {
            new() { Series = 589, BPLID = 1, PeriodIndicator = "FY26-27", Locked = "tNO" },
            new() { Series = 356, BPLID = 3, PeriodIndicator = "FY25-26", Locked = "tNO" },
        };

        SapDocumentSeriesResolver.FindSeries(series, bplId: 3, periodIndicator: "FY26-27")
            .Should().BeNull();
    }

    [Test]
    public void FormatMissingSeriesMessage_MentionsDocumentBplAndPeriod()
    {
        var msg = SapDocumentSeriesResolver.FormatMissingSeriesMessage(
            "A/P Down Payment Request (ODPO)", 3, "FY26-27", new DateTime(2026, 8, 8));
        msg.Should().Contain("A/P Down Payment Request (ODPO)");
        msg.Should().Contain("BPL 3");
        msg.Should().Contain("FY26-27");
        msg.Should().Contain("2026-08-08");
        msg.Should().Contain("Document Numbering");
    }

    [Test]
    public void FindSeries_MatchesPurchaseDownPaymentFy26Bpl1()
    {
        // LIVE ODPO series 694 (PB26-27) for BPL1 FY26-27 — same selection rules as OPOR.
        var series = new List<SapDocumentSeriesEntry>
        {
            new() { Series = 690, BPLID = 1, PeriodIndicator = "FY25-26", Locked = "tNO", Name = "PB25-26" },
            new() { Series = 694, BPLID = 1, PeriodIndicator = "FY26-27", Locked = "tNO", Name = "PB26-27" },
            new() { Series = 700, BPLID = 2, PeriodIndicator = "FY26-27", Locked = "tNO", Name = "other-bpl" },
        };

        var match = SapDocumentSeriesResolver.FindSeries(series, bplId: 1, periodIndicator: "FY26-27");
        match.Should().NotBeNull();
        match!.Series.Should().Be(694);
        match.Name.Should().Be("PB26-27");
    }
}
