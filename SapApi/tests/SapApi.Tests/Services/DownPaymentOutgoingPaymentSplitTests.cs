using FluentAssertions;
using SapApi.Infrastructure.Services;
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
        var basicNet = StageWisePaymentCalculations.NetDownPaymentApplication(gross, isGst: false, tds);
        var gstNet = StageWisePaymentCalculations.NetDownPaymentApplication(gst, isGst: true, tds: 0);
        var transferSum = Math.Round(basicNet + gstNet, 2);

        basicNet.Should().Be(expectedBasicNet);
        gstNet.Should().Be(expectedGstNet);
        transferSum.Should().Be(expectedBasicNet + expectedGstNet);
    }

    [Test]
    public void NetOutgoing_DeductsTdsOnEachBasicDownPayment()
    {
        var first = StageWisePaymentCalculations.NetDownPaymentApplication(10000, isGst: false, tds: 100);
        var second = StageWisePaymentCalculations.NetDownPaymentApplication(5000, isGst: false, tds: 50);
        var gst = StageWisePaymentCalculations.NetDownPaymentApplication(1800, isGst: true, tds: 0);

        first.Should().Be(9900);
        second.Should().Be(4950);
        gst.Should().Be(1800);
        Math.Round(first + second + gst, 2).Should().Be(16650);
    }
}
