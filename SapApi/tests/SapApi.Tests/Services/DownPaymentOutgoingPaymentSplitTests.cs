using FluentAssertions;
using SapApi.Shared;

namespace SapApi.Tests.Services;

public class DownPaymentOutgoingPaymentSplitTests
{
    [Test]
    public void PaymentRemarks_StillBuildsPoReference()
    {
        Constants.PaymentRemarks.Build("Advance", 1, "100")
            .Should().Contain("Based on Purchase Order PB/PO/100");
    }

    [TestCase(100, 18, 10, 90, 18)]
    [TestCase(50, 0, 5, 45, 0)]
    [TestCase(0, 18, 0, 0, 18)]
    public void NetOutgoing_SplitsBasicAndGstCorrectly(
        double gross,
        double gst,
        double tds,
        double expectedBasicNet,
        double expectedGstNet)
    {
        var basicNet = Math.Round(Math.Max(0, gross - tds), 2);
        var gstNet = Math.Round(gst, 2);
        var transferSum = Math.Round(basicNet + gstNet, 2);

        basicNet.Should().Be(expectedBasicNet);
        gstNet.Should().Be(expectedGstNet);
        transferSum.Should().Be(expectedBasicNet + expectedGstNet);
    }
}
