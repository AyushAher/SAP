using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using SapApi.Domain.Interfaces;
using SapApi.Domain.Models;
using SapApi.Shared.Enums;

namespace SapApi.Infrastructure.Caching;

public class DistributedCacheSapSessionStore(IDistributedCache cache) : ISapSessionStore
{
    private static string SessionKey(int userId, SapCompanyDatabase companyDb) => $"sap:session:{userId}:{companyDb}";
    private static string CredentialsKey(int userId, SapCompanyDatabase companyDb) => $"sap:cred:{userId}:{companyDb}";

    public async Task<SapSessionInfo?> GetSessionAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default) =>
        await GetAsync<SapSessionInfo>(SessionKey(userId, companyDb), cancellationToken);

    public async Task<SapRenewalCredentials?> GetCredentialsAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default) =>
        await GetAsync<SapRenewalCredentials>(CredentialsKey(userId, companyDb), cancellationToken);

    public async Task SetSessionAsync(int userId, SapCompanyDatabase companyDb, SapSessionInfo session, SapRenewalCredentials credentials, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await SetAsync(SessionKey(userId, companyDb), session, options, cancellationToken);
        await SetAsync(CredentialsKey(userId, companyDb), credentials, options, cancellationToken);
    }

    public async Task RemoveSessionAsync(int userId, SapCompanyDatabase companyDb, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(SessionKey(userId, companyDb), cancellationToken);
        await cache.RemoveAsync(CredentialsKey(userId, companyDb), cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var data = await cache.GetAsync(key, cancellationToken);
        if (data is null || data.Length == 0)
            return default;

        var json = Decompress(data);
        return JsonSerializer.Deserialize<T>(json);
    }

    private async Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        await cache.SetAsync(key, Compress(json), options, cancellationToken);
    }

    private static byte[] Compress(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Fastest))
            gzip.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }

    private static string Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
