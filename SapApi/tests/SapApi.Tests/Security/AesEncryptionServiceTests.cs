using FluentAssertions;
using Microsoft.Extensions.Options;
using SapApi.Infrastructure.Security;
using SapApi.Shared.Configuration;

namespace SapApi.Tests.Security;

[TestFixture]
public class AesEncryptionServiceTests
{
    private AesEncryptionService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new AesEncryptionService(Options.Create(new SecurityOptions
        {
            AesKeyBase64 = Convert.ToBase64String(new byte[32])
        }));
    }

    [Test]
    public void EncryptDecrypt_RoundTrips_PlainText()
    {
        const string plain = "sensitive-bank-account-4644008700000209";
        var encrypted = _sut.Encrypt(plain);
        encrypted.Should().NotBe(plain);
        _sut.Decrypt(encrypted).Should().Be(plain);
    }

    [Test]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        _sut.Encrypt("").Should().Be("");
    }
}
