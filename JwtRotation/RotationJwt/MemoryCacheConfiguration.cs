using Microsoft.Extensions.Caching.Memory;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RotatingJwt
{
    /// <summary>
    /// Added a Configuration for use memory cache
    /// </summary>
    public class MemoryCacheConfiguration(IMemoryCache memoryCache) : ICacheConfiguration
    {
        /// <summary>
        /// Retrieves an object from the memory cache using a generated key.
        /// </summary>
        /// <typeparam name="T">The type of the object to retrieve.</typeparam>
        /// <param name="cacheKey">Retrives the data using the cacheKey.</param>
        /// <returns>The cached object if found; otherwise, <c>null</c>.</returns>
        public Task<T?> GetFromCacheMemoryByIdAsync<T>(string cacheKey)
        {

            //var cacheKey = GenerateCacheKey(obj);

            memoryCache.TryGetValue(cacheKey, out var cacheValue);
            var result = string.IsNullOrEmpty(cacheValue?.ToString())
                ? default
                : JsonSerializer.Deserialize<T>(cacheValue.ToString() ?? "{}");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Stores an object in the memory cache with an optional expiration time.
        /// </summary>
        /// <typeparam name="T">The type of object to cache.</typeparam>
        /// <param name="obj">The object to store in the cache.</param>
        /// <param name="cacheKey">The object store in the cache against cacheKey.</param>
        /// <param name="expiry">The expiration time in minutes (default is 5 minutes).</param>
        /// <returns>A completed task.</returns>
        public Task SetInCacheMemoryAsync<T>(string cacheKey, T? obj, int? expiry = 5)
        {
            //var cacheKey = GenerateCacheKey(obj);
            MemoryCacheEntryOptions? cacheEntryOptions = null;
            var value = JsonSerializer.Serialize(obj);
            if (expiry is not null)
            {
                // Set expiration time
                cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiry.Value)
                };
            }

            memoryCache.Set(cacheKey, value, cacheEntryOptions);
            return Task.CompletedTask;
        }
    }
}