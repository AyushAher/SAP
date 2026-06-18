using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Infrastructure.Security;
using SapApi.Shared.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace SapApi.Tests.Security;

[TestFixture]
public class RsaDecryptionServiceTests
{
    [Test]
    public void Decrypt_Pkcs1EncryptedText_ReturnsOriginal()
    {
        using var rsa = RSA.Create(2048);
        var plain = "test-password-123";
        var cipher = Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(plain), RSAEncryptionPadding.Pkcs1));

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var privatePath = Path.Combine(tempDir, "private.pem");
        var publicPath = Path.Combine(tempDir, "public.pem");
        File.WriteAllText(privatePath, rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(publicPath, rsa.ExportRSAPublicKeyPem());

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(tempDir);

        var sut = new RsaDecryptionService(env.Object, Options.Create(new SecurityOptions
        {
            RsaPrivateKeyPath = "private.pem",
            RsaPublicKeyPath = "public.pem"
        }));

        sut.Decrypt(cipher).Should().Be(plain);
    }
}
