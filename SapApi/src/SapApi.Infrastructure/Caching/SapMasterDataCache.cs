using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using SapApi.Domain.Interfaces;
using Serilog;

namespace SapApi.Infrastructure.Caching;

/// <summary>
/// <see cref="IDistributedCache"/>-backed implementation of <see cref="ISapMasterDataCache"/>. Values
/// are JSON-serialized and gzip-compressed before being stored, matching the approach already used for
/// SAP session caching (see <see cref="DistributedCacheSapSessionStore"/>).
/// </summary>
public class SapMasterDataCache(IDistributedCache cache) : ISapMasterDataCache
{
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var cached = await TryGetAsync<T>(key, cancellationToken);
        if (cached.HasValue)
            return cached.Value;

        var value = await factory();
        if (value is not null)
            await SetAsync(key, value, ttl, cancellationToken);

        return value;
    }

    private async Task<(bool HasValue, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var data = await cache.GetAsync(key, cancellationToken);
            if (data is null || data.Length == 0)
                return (false, default);

            var json = Decompress(data);
            return (true, JsonSerializer.Deserialize<T>(json));
        }
        catch (Exception ex)
        {
            // A corrupt/incompatible cache entry (e.g. after a DTO shape change) should never break the
            // request — fall through to the live SAP call instead.
            Log.Warning(ex, "Failed to read master-data cache entry {CacheKey}; falling back to SAP", key);
            return (false, default);
        }
    }

    private async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            await cache.SetAsync(key, Compress(json), options, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write master-data cache entry {CacheKey}", key);
        }
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
