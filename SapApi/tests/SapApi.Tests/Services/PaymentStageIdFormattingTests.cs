using SapApi.Infrastructure.Services;

namespace SapApi.Tests.Services;

public class PaymentStageIdFormattingTests
{
    [TestCase(null, null)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(3, "3")]
    public void FormatPaymentStageId_Single(int? id, string? expected)
    {
        Assert.That(StageWisePaymentService.FormatPaymentStageId(id), Is.EqualTo(expected));
    }

    [Test]
    public void FormatPaymentStageId_Multiple_DedupesAndSorts()
    {
        Assert.That(StageWisePaymentService.FormatPaymentStageId([5, 1, 3, 1, 0]), Is.EqualTo("1,3,5"));
    }

    [Test]
    public void FormatPaymentStageId_Empty_ReturnsNull()
    {
        Assert.That(StageWisePaymentService.FormatPaymentStageId(Array.Empty<int>()), Is.Null);
    }
}
