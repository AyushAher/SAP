#if USEREDIS
using Serilog;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RotatingJwt
{
    /// <summary>
    /// Added a Configuration for use redis cache
    /// </summary>
    public class RedisCacheConfiguration(
        IConnectionMultiplexer connectionMultiplexer) : ICacheConfiguration
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer
                                                                         ?? throw new ArgumentNullException(
                                                                             nameof(connectionMultiplexer));
        /// <summary>
        /// Retrieves an object from the Redis cache based on its key.
        /// </summary>
        /// <typeparam name="T">The type of object to retrieve.</typeparam>
        /// <param name="cacheKey">Get the data using cacheKey.</param>
        /// <returns>The retrieved object from the cache, or default if not found.</returns>
        public async Task<T?> GetFromCacheMemoryByIdAsync<T>(string cacheKey)
        {
            try
            {
                //var cacheKey = GenerateCacheKey(obj);
                var cacheValue = await GetDatabase().HashGetAsync(typeof(T).Name, cacheKey);
                return cacheValue.HasValue ? Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cacheValue!) : default;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to get object from cache.");
                return default;
            }
        }

        /// <summary>
        /// Stores an object in the Redis cache with an optional expiry time.
        /// </summary>
        /// <typeparam name="T">The type of object to store.</typeparam>
        /// <param name="obj">The object to store in the cache.</param>
        /// <param name="cacheKey">The object store in the cache against cacheKey.</param>
        /// <param name="expiry">The expiry time in minutes (default is 5 minutes).</param>
        public async Task SetInCacheMemoryAsync<T>(string cacheKey, T? obj, int? expiry = 5)
        {
            try
            {
                //var cacheKey = GenerateCacheKey(obj);
                var serializedObj = JsonSerializer.Serialize(obj);
                var hashEntry = new HashEntry(cacheKey, serializedObj);

                await GetDatabase().HashSetAsync(typeof(T).Name, [hashEntry]);

                if (expiry.HasValue)
                {
                    var expirationTimeSpan = TimeSpan.FromMinutes(expiry.Value);
                    await GetDatabase().KeyExpireAsync(cacheKey, expirationTimeSpan);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to set object in cache.");
            }
        }

        private IDatabase GetDatabase()
           => _connectionMultiplexer.GetDatabase(1);

    }
}

#endif