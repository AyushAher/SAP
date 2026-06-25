using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Configuration;

namespace SapApi.Infrastructure.Security;

public class RsaDecryptionService : IRsaDecryptionService
{
    private readonly RSA _rsa;
    private readonly string _publicKeyPem;

    public RsaDecryptionService(IHostEnvironment env, IOptions<SecurityOptions> options)
    {
        var privateKeyPath = Path.Combine(env.ContentRootPath, options.Value.RsaPrivateKeyPath);
        var publicKeyPath = Path.Combine(env.ContentRootPath, options.Value.RsaPublicKeyPath);

        _rsa = RSA.Create();
        if (File.Exists(privateKeyPath))
        {
            _rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
            // Always publish the public key that matches the loaded private key.
            _publicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
        }
        else
        {
            _publicKeyPem = File.Exists(publicKeyPath) ? File.ReadAllText(publicKeyPath) : string.Empty;
        }
    }

    public string Decrypt(string cipherTextBase64)
    {
        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = _rsa.Decrypt(cipherBytes, RSAEncryptionPadding.Pkcs1);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public string GetPublicKeyPem() => _publicKeyPem;
}
