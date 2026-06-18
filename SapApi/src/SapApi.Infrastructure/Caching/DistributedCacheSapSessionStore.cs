using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using SapApi.Domain.Interfaces;
using SapApi.Domain.Models;

namespace SapApi.Infrastructure.Caching;

public class DistributedCacheSapSessionStore(IDistributedCache cache) : ISapSessionStore
{
    private static string SessionKey(int userId) => $"sap:session:{userId}";
    private static string CredentialsKey(int userId) => $"sap:cred:{userId}";

    public async Task<SapSessionInfo?> GetSessionAsync(int userId, CancellationToken cancellationToken = default) =>
        await GetAsync<SapSessionInfo>(SessionKey(userId), cancellationToken);

    public async Task<SapRenewalCredentials?> GetCredentialsAsync(int userId, CancellationToken cancellationToken = default) =>
        await GetAsync<SapRenewalCredentials>(CredentialsKey(userId), cancellationToken);

    public async Task SetSessionAsync(int userId, SapSessionInfo session, SapRenewalCredentials credentials, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await SetAsync(SessionKey(userId), session, options, cancellationToken);
        await SetAsync(CredentialsKey(userId), credentials, options, cancellationToken);
    }

    public async Task RemoveSessionAsync(int userId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(SessionKey(userId), cancellationToken);
        await cache.RemoveAsync(CredentialsKey(userId), cancellationToken);
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
