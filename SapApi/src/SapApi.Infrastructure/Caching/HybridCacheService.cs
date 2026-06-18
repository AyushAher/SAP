using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;

namespace SapApi.Infrastructure.Caching;

/// <summary>
/// Two-tier cache: L1 in-memory (fast) + L2 PostgreSQL (durable). No Redis required.
/// </summary>
public class HybridCacheService(IMemoryCache memoryCache, AppDbContext dbContext) : ICacheService
{
    private static readonly MemoryCacheEntryOptions MemoryOptions = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(key, out T? cached))
            return cached;

        var entry = await dbContext.CacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        if (entry == null || entry.ExpiresAtUtc <= DateTime.UtcNow)
            return default;

        var json = Decompress(entry.CompressedValue);
        var value = JsonSerializer.Deserialize<T>(json);
        if (value != null)
            memoryCache.Set(key, value, MemoryOptions);

        return value;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        memoryCache.Set(key, value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = ttl });

        var json = JsonSerializer.Serialize(value);
        var compressed = Compress(json);
        var expiresAt = DateTime.UtcNow.Add(ttl);

        var existing = await dbContext.CacheEntries.FindAsync([key], cancellationToken);
        if (existing == null)
        {
            await dbContext.CacheEntries.AddAsync(new CacheEntry
            {
                Key = key,
                CompressedValue = compressed,
                ExpiresAtUtc = expiresAt
            }, cancellationToken);
        }
        else
        {
            existing.CompressedValue = compressed;
            existing.ExpiresAtUtc = expiresAt;
            dbContext.CacheEntries.Update(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);
        var entry = await dbContext.CacheEntries.FindAsync([key], cancellationToken);
        if (entry != null)
        {
            dbContext.CacheEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static byte[] Compress(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Fastest))
            gzip.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }

    public static string Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
