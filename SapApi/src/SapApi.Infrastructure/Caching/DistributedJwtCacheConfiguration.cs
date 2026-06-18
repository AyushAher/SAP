using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using RotatingJwt;

namespace SapApi.Infrastructure.Caching;

/// <summary>
/// Redis-backed JWT refresh token and key cache for multi-instance deployments.
/// Replaces RotatingJwt in-memory cache when Redis is configured.
/// </summary>
public class DistributedJwtCacheConfiguration(IDistributedCache cache) : ICacheConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T?> GetFromCacheMemoryByIdAsync<T>(string cacheKey)
    {
        var data = await cache.GetAsync(cacheKey);
        if (data is null || data.Length == 0)
            return default;

        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data), JsonOptions);
    }

    public async Task SetInCacheMemoryAsync<T>(string cacheKey, T? obj, int? expiry = 5)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiry is not null)
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiry.Value);

        var json = JsonSerializer.Serialize(obj, JsonOptions);
        await cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json), options);
    }
}
