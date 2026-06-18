using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Configuration;

namespace SapApi.Infrastructure.Security;

public class HmacVerificationService : IHmacVerificationService
{
    private readonly byte[] _secret;

    public HmacVerificationService(IOptions<SecurityOptions> options)
    {
        var secret = options.Value.HmacSecret;
        _secret = string.IsNullOrWhiteSpace(secret)
            ? Encoding.UTF8.GetBytes("SapApi-Hmac-Secret-Change-In-Production")
            : Encoding.UTF8.GetBytes(secret);
    }

    public string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var expected = ComputeSignature(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}
