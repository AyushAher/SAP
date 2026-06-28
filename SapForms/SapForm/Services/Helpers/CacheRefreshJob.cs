using Serilog;
using static Shared.Constants;

namespace SapForm.Services.Helpers
{
    public class CacheRefreshJob(IHttpRequestHandler http, RedisCacheService redis)
    {
        public async Task RefreshAsync()
        {
            Task.WaitAll(CachedEndpoints.Endpoints.Select(SetInCache));
        }

        private async Task SetInCache(string endpoint)
        {
            try
            {
                Log.Information("Refreshing cache: {Endpoint}", endpoint);
                var data = await http.GetAsync<object>(endpoint, false, false);
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                Log.Information("Cached {Endpoint} size: {Size} KB", endpoint, json.Length / 1024);
                if (data is not null)
                {
                    await redis.SetAsync(
                        $"GET::{endpoint}",
                        data,
                        TimeSpan.FromDays(1)
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Cache refresh failed: {Endpoint}", endpoint);
            }

        }
    }
}