using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class StageWisePaymentTdsRuleTests
{
    private static readonly List<PaymentTermsUdf> Terms =
    [
        new() { Id = 1, Gst = 100, Basic = 0, Type = "GstProforma" },
        new() { Id = 2, Basic = 80, Gst = 0, Type = "Invoice" },
    ];

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
    public void HasPriorInvoiceSelectedPayment_IgnoresDownPaymentsWithoutInvoice()
    {
        StageWisePaymentCalculations.HasPriorInvoiceSelectedPayment(
        [
            new StageWisePayment
            {
                Status = StageWisePaymentStatus.Added,
                Tds = 2,
                ApInvoiceDocEntry = null,
            },
        ]).Should().BeFalse();

        StageWisePaymentCalculations.HasPriorInvoiceSelectedPayment(
        [
            new StageWisePayment
            {
                Status = StageWisePaymentStatus.Added,
                Tds = 0,
                ApInvoiceDocEntry = "5507",
            },
        ]).Should().BeTrue();
    }

    [Test]
    public void InvoiceKeysOverlap_MatchesCommaJoinedEntries()
    {
        StageWisePaymentCalculations.InvoiceKeysOverlap("10,20", "20").Should().BeTrue();
        StageWisePaymentCalculations.InvoiceKeysOverlap("10", "11").Should().BeFalse();
    }

    [Test]
    public void SkipInvoiceWithholding_OnGstOnlyAndOnSameInvoiceAfterTdsTaken()
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);

        StageWisePaymentCalculations.SkipInvoiceWithholding([], Terms, [1], "55", applied)
            .Should().BeTrue();

        applied.Clear();
        StageWisePaymentCalculations.SkipInvoiceWithholding(
                [new StageWisePayment { Status = StageWisePaymentStatus.Added, Tds = 41.5, ApInvoiceDocEntry = "55" }],
                Terms,
                [2],
                "55",
                applied)
            .Should().BeTrue();

        applied.Clear();
        StageWisePaymentCalculations.SkipInvoiceWithholding([], Terms, [2], "55", applied)
            .Should().BeFalse();
    }

    [Test]
    public void SkipInvoiceWithholding_TakesWtOnSecondInvoiceOfSamePo()
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        var priorOtherInvoice = new StageWisePayment
        {
            Status = StageWisePaymentStatus.Added,
            Tds = 1450,
            ApInvoiceDocEntry = "9808",
        };

        StageWisePaymentCalculations.SkipInvoiceWithholding(
                [priorOtherInvoice],
                Terms,
                [2],
                "9809",
                applied)
            .Should().BeFalse();
    }

    [Test]
    public void SkipInvoiceWithholding_AllowsInvoiceTdsAfterPriorDownPayment()
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        var priorDownPayment = new StageWisePayment
        {
            Status = StageWisePaymentStatus.Added,
            StageDesc = "Batch down payment",
            Tds = 2,
            ApInvoiceDocEntry = null,
        };

        StageWisePaymentCalculations.SkipInvoiceWithholding(
                [priorDownPayment],
                Terms,
                [2],
                "5507",
                applied)
            .Should().BeFalse();
    }

    [Test]
    public void ComputeApInvoiceTdsAmount_TakesInvoiceWtOnFirstInvoiceRequestAfterDownPayment()
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        var invoice = new SapPurchaseInvoicesResponse { WTAmount = 41.5 };
        var priorDownPayment = new StageWisePayment
        {
            Status = StageWisePaymentStatus.Added,
            Tds = 2,
            ApInvoiceDocEntry = null,
        };

        StageWisePaymentCalculations.ComputeApInvoiceTdsAmount(
                invoice, [priorDownPayment], Terms, [2], "5507", applied)
            .Should().Be(41.5);

        StageWisePaymentCalculations.ComputeApInvoiceTdsAmount(
                invoice,
                [
                    priorDownPayment,
                    new StageWisePayment
                    {
                        Status = StageWisePaymentStatus.Added,
                        Tds = 41.5,
                        ApInvoiceDocEntry = "5507",
                    },
                ],
                Terms,
                [2],
                "5507",
                new HashSet<string>(StringComparer.Ordinal))
            .Should().Be(0);

        StageWisePaymentCalculations.ComputeApInvoiceTdsAmount(
                invoice, [], Terms, [1], "5507", new HashSet<string>(StringComparer.Ordinal))
            .Should().Be(0);
    }

    [Test]
    public void BuildApInvoicePaymentApplications_GroupsRowsAndDeductsInvoiceWtOnce()
    {
        var invoice = new SapPurchaseInvoicesResponse
        {
            DocEntry = 5510,
            DocTotal = 86727,
            PaidToDate = 0,
            WTAmount = 74,
        };
        var lines = new List<(StageWisePaymentBatchLineRequest Line, SapPurchaseInvoicesResponse Invoice)>
        {
            (new() { Amount = 73560, PaymentTermsTypes = [2], ApInvoiceDocEntry = "5510" }, invoice),
            (new() { Amount = 13240, PaymentTermsTypes = [1], ApInvoiceDocEntry = "5510" }, invoice),
        };

        var apps = StageWisePaymentCalculations.BuildApInvoicePaymentApplications(lines, [], Terms);

        apps.Should().HaveCount(1);
        apps[0].DocEntry.Should().Be(5510);
        apps[0].GrossAmount.Should().Be(86800);
        apps[0].Tds.Should().Be(74);
        apps[0].SumApplied.Should().Be(86726);
    }

    [Test]
    public void BuildApInvoicePaymentApplications_StillTakesThisInvoiceWtAfterOtherInvoiceOnPo()
    {
        var invoice = new SapPurchaseInvoicesResponse
        {
            DocEntry = 5510,
            DocTotal = 86727,
            PaidToDate = 0,
            WTAmount = 74,
        };
        var priorOtherInvoice = new StageWisePayment
        {
            Status = StageWisePaymentStatus.Added,
            Tds = 302,
            ApInvoiceDocEntry = "5512",
        };
        var lines = new List<(StageWisePaymentBatchLineRequest Line, SapPurchaseInvoicesResponse Invoice)>
        {
            (new() { Amount = 73560, PaymentTermsTypes = [2], ApInvoiceDocEntry = "5510" }, invoice),
            (new() { Amount = 13240, PaymentTermsTypes = [1], ApInvoiceDocEntry = "5510" }, invoice),
        };

        var apps = StageWisePaymentCalculations.BuildApInvoicePaymentApplications(
            lines, [priorOtherInvoice], Terms);

        apps.Should().HaveCount(1);
        apps[0].Tds.Should().Be(74);
        apps[0].SumApplied.Should().Be(86726);
    }
}
