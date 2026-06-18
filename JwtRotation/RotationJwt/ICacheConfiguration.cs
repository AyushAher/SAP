using System.Threading.Tasks;

namespace RotatingJwt
{
    /// <summary>
    /// Added a common interface for Memory and Redis cache configuration.
    /// </summary>
    public interface ICacheConfiguration
    {
        /// <summary>
        /// Upserts Record in the Cache Memory
        /// </summary>
        /// <param name="expiry">Expiry in Minutes. The cache will clear after the given expiry.</param>
        /// <param name="obj">Model Class</param>
        /// <param name="cacheKey">The object store in the cache against cacheKey.</param>
        public Task SetInCacheMemoryAsync<T>(string cacheKey, T? obj, int? expiry = 5);

        /// <summary>
        /// Get Record from Cache Memory of that particular key using id
        /// </summary>
        /// <param name="cacheKey">Model Class</param>
        /// <returns>Model Class with data</returns>
        public Task<T?> GetFromCacheMemoryByIdAsync<T>(string cacheKey);
    }
}
