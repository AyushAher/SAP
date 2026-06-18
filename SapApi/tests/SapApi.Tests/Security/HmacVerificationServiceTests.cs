using FluentAssertions;
using Microsoft.Extensions.Options;
using SapApi.Infrastructure.Security;
using SapApi.Shared.Configuration;

namespace SapApi.Tests.Security;

[TestFixture]
public class HmacVerificationServiceTests
{
    private HmacVerificationService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new HmacVerificationService(Options.Create(new SecurityOptions
        {
            HmacSecret = "test-hmac-secret"
        }));
    }

    [Test]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        const string payload = """{"userName":"admin","password":"secret"}""";
        var signature = _sut.ComputeSignature(payload);
        _sut.VerifySignature(payload, signature).Should().BeTrue();
    }

    [Test]
    public void VerifySignature_TamperedPayload_ReturnsFalse()
    {
        const string payload = """{"userName":"admin"}""";
        var signature = _sut.ComputeSignature(payload);
        _sut.VerifySignature("""{"userName":"hacker"}""", signature).Should().BeFalse();
    }
}
