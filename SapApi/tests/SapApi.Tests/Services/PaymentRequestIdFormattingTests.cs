using SapApi.Infrastructure.Services;

namespace SapApi.Tests.Services;

public class PaymentRequestIdFormattingTests
{
    [TestCase(null, null)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(42, "42")]
    public void FormatPaymentRequestId(int? id, string? expected)
    {
        Assert.That(StageWisePaymentService.FormatPaymentRequestId(id), Is.EqualTo(expected));
    }
}
