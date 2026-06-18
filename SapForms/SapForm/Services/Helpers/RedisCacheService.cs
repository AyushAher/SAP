using Microsoft.Extensions.Caching.Distributed;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SapForm.Services.Helpers
{

    public class RedisCacheService(IDistributedCache cache)
    {
        public async Task<T?> GetAsync<T>(string key)
        {
            var data = await cache.GetAsync(key);
            if (data is null) return default;
            var decompressedData = Decompress(data);
            return JsonSerializer.Deserialize<T>(decompressedData);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
        {
            await cache.RemoveAsync(key);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            await cache.SetAsync(
                key,
    Compress(JsonSerializer.Serialize(value)),
                options
            );
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


}
