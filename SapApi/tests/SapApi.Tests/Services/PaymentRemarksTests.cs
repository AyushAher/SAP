using FluentAssertions;
using SapApi.Shared;

namespace SapApi.Tests.Services;

public class PaymentRemarksTests
{
    [TestCase(1, "PB")]
    [TestCase(3, "SM")]
    [TestCase(4, "DE")]
    [TestCase(5, "PE")]
    public void Build_UsesConfiguredBranchCode(int bplId, string expectedBranchCode)
    {
        var result = Constants.PaymentRemarks.Build(null, bplId, "1234");

        result.Should().Be($"Based on Purchase Order {expectedBranchCode}/PO/1234");
    }

    [Test]
    public void Build_PrependsUserRemark()
    {
        var result = Constants.PaymentRemarks.Build("Release after inspection", 1, "1234");

        result.Should().Be(
            $"Release after inspection{Environment.NewLine}Based on Purchase Order PB/PO/1234");
    }
}
