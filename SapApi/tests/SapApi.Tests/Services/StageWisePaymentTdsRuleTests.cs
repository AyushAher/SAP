using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class StageWisePaymentTdsRuleTests
{
    [Test]
    public void IsGstOnlyTerm_WhenBasicIsZeroAndGstIsSet()
    {
        StageWisePaymentCalculations.IsGstOnlyTerm(new PaymentTermsUdf { Id = 1, Gst = 100, Basic = 0 })
            .Should().BeTrue();
        StageWisePaymentCalculations.IsGstOnlyTerm(new PaymentTermsUdf { Id = 1, Basic = 80, Gst = 18 })
            .Should().BeFalse();
    }

    [Test]
    public void TdsAlreadyTaken_IgnoresCancelledRows()
    {
        StageWisePaymentCalculations.TdsAlreadyTaken(
        [
            new StageWisePayment { Status = StageWisePaymentStatus.Cancelled, Tds = 50 },
            new StageWisePayment { Status = StageWisePaymentStatus.Added, Tds = 0 },
        ]).Should().BeFalse();

        StageWisePaymentCalculations.TdsAlreadyTaken(
        [
            new StageWisePayment { Status = StageWisePaymentStatus.Added, Tds = 12 },
        ]).Should().BeTrue();
    }

    [Test]
    public void HasPriorActivePayment_TreatsAnyNonCancelledRowAsPrior()
    {
        StageWisePaymentCalculations.HasPriorActivePayment(
        [
            new StageWisePayment { Status = StageWisePaymentStatus.Cancelled, Tds = 0 },
        ]).Should().BeFalse();

        StageWisePaymentCalculations.HasPriorActivePayment(
        [
            new StageWisePayment { Status = StageWisePaymentStatus.Added, Tds = 0 },
        ]).Should().BeTrue();
    }

    [Test]
    public void InvoiceKeysOverlap_MatchesCommaJoinedEntries()
    {
        StageWisePaymentCalculations.InvoiceKeysOverlap("10,20", "20").Should().BeTrue();
        StageWisePaymentCalculations.InvoiceKeysOverlap("10", "11").Should().BeFalse();
    }

    [Test]
    public void SkipInvoiceWithholding_OnGstOnlyAndOnSecondRequest()
    {
        var terms = new List<PaymentTermsUdf>
        {
            new() { Id = 1, Gst = 100, Basic = 0, Type = "GstProforma" },
            new() { Id = 2, Basic = 80, Gst = 0 },
        };
        var applied = new HashSet<string>(StringComparer.Ordinal);

        StageWisePaymentCalculations.SkipInvoiceWithholding([], terms, [1], "55", applied)
            .Should().BeTrue();

        applied.Clear();
        StageWisePaymentCalculations.SkipInvoiceWithholding(
                [new StageWisePayment { Status = StageWisePaymentStatus.Added, Tds = 0 }],
                terms,
                [2],
                "55",
                applied)
            .Should().BeTrue();

        applied.Clear();
        StageWisePaymentCalculations.SkipInvoiceWithholding([], terms, [2], "55", applied)
            .Should().BeFalse();
    }
}
