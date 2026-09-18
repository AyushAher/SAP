using SapForm.Services.Login;
using Serilog;
using Shared.Responses.Sap;
using System.Text;
using System.Text.Json;
using static Shared.Constants;

namespace SapForm.Services.Helpers
{
    public class HttpRequestHandler(IServiceProvider serviceProvider, HttpClient client, RedisCacheService redis) : IHttpRequestHandler
    {
        public async Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true)
        {
            var cacheKey = $"GET::{url}";
            try
            {
                Log.Information(url);

                //  Redis lookup
                if (checkCache && url.StartsWith(SapServiceLayerUrl))
                {
                    T? cached = await redis.GetAsync<T>(cacheKey);
                    if (cached is not null)
                    {
                        Log.Information("Cache hit: {Url}", url);
                        return cached;
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (setTimeout)
                    client.Timeout = TimeSpan.FromHours(1);
                if (url.StartsWith(SapServiceLayerUrl))
                {
                    var sessionId = await EnsureSapSessionAsync();
                    SetSessionCookie(request, sessionId);
                    request.Headers.Add("Prefer", "odata.maxpagesize=0");
                }

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (IsInvalidSapSession(json) && url.StartsWith(SapServiceLayerUrl))
                    {
                        var sessionId = await EnsureSapSessionAsync(forceRefresh: true);
                        var retry = new HttpRequestMessage(HttpMethod.Get, url);
                        SetSessionCookie(retry, sessionId);
                        retry.Headers.Add("Prefer", "odata.maxpagesize=0");
                        response = await client.SendAsync(retry);
                        if (response.IsSuccessStatusCode)
                            return await response.Content.ReadFromJsonAsync<T>();
                        json = await response.Content.ReadAsStringAsync();
                    }

                    if (typeof(T).IsAssignableTo(typeof(SapBaseResponse)))
                        return JsonSerializer.Deserialize<T>(json);
                    throw new ApiErrorException(json);
                }
                T? result = await response.Content.ReadFromJsonAsync<T>();

                // Save to Redis (1 day)
                if (CachedEndpoints.ShouldCache(url) && result is not null)
                {
                    await redis.SetAsync(cacheKey, result, TimeSpan.FromHours(6));
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GET failed");
                return default;
            }
        }
        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                if (url.StartsWith(SapServiceLayerUrl))
                {
                    SetSessionCookie(request, await EnsureSapSessionAsync());
                    request.Headers.TransferEncodingChunked = false;
                    request.Headers.ExpectContinue = false;
                }

                var json = JsonSerializer.Serialize(data);
                request.Content = JsonBytesContent(json);

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    if (IsInvalidSapSession(errorJson) && url.StartsWith(SapServiceLayerUrl))
                    {
                        var retry = new HttpRequestMessage(HttpMethod.Put, url);
                        SetSessionCookie(retry, await EnsureSapSessionAsync(forceRefresh: true));
                        retry.Headers.TransferEncodingChunked = false;
                        retry.Headers.ExpectContinue = false;
                        retry.Content = JsonBytesContent(json);
                        response = await client.SendAsync(retry);
                        if (response.IsSuccessStatusCode)
                            return await ReadSuccessAsync<TResponse>(response);
                        errorJson = await response.Content.ReadAsStringAsync();
                    }

                    if (typeof(TResponse).IsAssignableTo(typeof(SapBaseResponse)))
                        return JsonSerializer.Deserialize<TResponse>(errorJson);
                    throw new ApiErrorException(errorJson);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return default;

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PUT failed");
                return default;
            }
        }

        public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, url);
                if (url.StartsWith(SapServiceLayerUrl))
                    SetSessionCookie(request, await EnsureSapSessionAsync());
                var patchJson = JsonSerializer.Serialize(data);
                request.Content = new StringContent(patchJson);

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (IsInvalidSapSession(json) && url.StartsWith(SapServiceLayerUrl))
                    {
                        var retry = new HttpRequestMessage(HttpMethod.Patch, url);
                        SetSessionCookie(retry, await EnsureSapSessionAsync(forceRefresh: true));
                        retry.Content = new StringContent(patchJson);
                        response = await client.SendAsync(retry);
                        if (response.IsSuccessStatusCode)
                            return await ReadSuccessAsync<TResponse>(response);
                        json = await response.Content.ReadAsStringAsync();
                    }

                    if (typeof(TResponse).IsAssignableTo(typeof(SapBaseResponse)))
                        return JsonSerializer.Deserialize<TResponse>(json);
                    throw new ApiErrorException(json);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }
        }
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (url.StartsWith(SapServiceLayerUrl))
                {
                    SetSessionCookie(request, await EnsureSapSessionAsync());
                    request.Headers.TransferEncodingChunked = false;
                    request.Headers.ExpectContinue = false;
                }
                string? postJson = null;
                if (data is not null)
                {
                    postJson = JsonSerializer.Serialize(data);
                    request.Content = JsonBytesContent(postJson);
                }

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TResponse>();

                var json = await response.Content.ReadAsStringAsync();
                if (IsInvalidSapSession(json) && url.StartsWith(SapServiceLayerUrl))
                {
                    var retry = new HttpRequestMessage(HttpMethod.Post, url);
                    SetSessionCookie(retry, await EnsureSapSessionAsync(forceRefresh: true));
                    retry.Headers.TransferEncodingChunked = false;
                    retry.Headers.ExpectContinue = false;
                    if (postJson is not null)
                        retry.Content = JsonBytesContent(postJson);
                    response = await client.SendAsync(retry);
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadFromJsonAsync<TResponse>();
                    json = await response.Content.ReadAsStringAsync();
                }

                if (typeof(TResponse).IsAssignableTo(typeof(SapBaseResponse)))
                    return JsonSerializer.Deserialize<TResponse>(json);
                throw new ApiErrorException(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }

        }
        public async Task PatchCachedEntityAsync<T>(string entity, int docEntry, string idProperty = "DocEntry")
        {
            var singleEndpoint = $"{SapServiceLayerUrl}{SapBaseUrl}/{entity}({docEntry})";
            var updated = await GetAsync<T>(singleEndpoint);
            if (updated == null) return;
            foreach (var endpoint in CachedEndpoints.Endpoints.Where(e => e.Contains(entity)))
            {
                var cacheKey = $"GET::{endpoint}";
                var cached = await redis.GetAsync<SapCacheResponse<T>>(cacheKey);

                if (cached == null || cached.Value is null) continue;

                var index = cached.Value.FindIndex(x =>
                    (int?)typeof(T).GetProperty(idProperty)?.GetValue(x) == docEntry);

                if (index >= 0)
                    cached.Value[index] = updated;
                else
                    cached.Value.Add(updated);

                await redis.SetAsync(cacheKey, cached, TimeSpan.FromHours(6));
            }
        }

        public async Task<T?> ExecuteSqlQueryAsync<T>(string queryName, Dictionary<string, object> parameters)
        {
            var sqlDetails = await GetSqlQueryDetailsAsync(queryName);
            if (sqlDetails == null)            
            {
                Log.Error("SQL query details not found for query: {QueryName}", queryName);
                throw new ApiErrorException($"SQL query details not found for query: {queryName}");
            }

            parameters.ToList().ForEach(kv =>
            {
                if (sqlDetails.ParamList?.Split(',').Select(s => s.Trim()).Any(p => p.Trim() == kv.Key) is null or false)
                {
                    Log.Error("Parameter {ParameterName} is not defined for SQL query {QueryName}", kv.Key, queryName);
                    throw new ApiErrorException($"Parameter {kv.Key} is not defined for SQL query {queryName}");
                }
            });

            var paramKeyValueString = string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            Log.Information("Executing SQL query: {QueryName} with parameters: {Parameters}", queryName, paramKeyValueString);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{SapServiceLayerUrl}{SapBaseUrl}/SQLQueries('{queryName}')/List");
            SetSessionCookie(request, await EnsureSapSessionAsync());
            
            var body = new { ParamList = paramKeyValueString };
            var json = JsonSerializer.Serialize(body);
            var bytes = Encoding.UTF8.GetBytes(json);
            var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentLength = bytes.Length;

            request.Content = content;

            HttpResponseMessage response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (typeof(T).IsAssignableTo(typeof(SapBaseResponse)))
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    SapBaseResponse? sapResult = JsonSerializer.Deserialize<SapBaseResponse>(responseContent);

                    if (sapResult?.Error?.Code == 301)
                    {
                        var retry = new HttpRequestMessage(HttpMethod.Post, $"{SapServiceLayerUrl}{SapBaseUrl}/SQLQueries('{queryName}')/List");
                        SetSessionCookie(retry, await EnsureSapSessionAsync(forceRefresh: true));
                        retry.Content = JsonBytesContent(json);
                        response = await client.SendAsync(retry);
                        if (response.IsSuccessStatusCode)
                            return await response.Content.ReadFromJsonAsync<T>();
                    }

                    return JsonSerializer.Deserialize<T>(responseContent);
                }
                else throw new ApiErrorException(await response.Content.ReadAsStringAsync());
            }
            var contentJson = await response.Content.ReadFromJsonAsync<T?>();
            return contentJson;
        }

        private async Task<SapQueryBaseResponse> GetSqlQueryDetailsAsync(string queryName)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SapServiceLayerUrl}{SapBaseUrl}/SQLQueries('{queryName}')");
            SetSessionCookie(request, await EnsureSapSessionAsync());
            HttpResponseMessage response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new ApiErrorException(await response.Content.ReadAsStringAsync());

            var responseJson = await response.Content.ReadFromJsonAsync<SapQueryBaseResponse>();
            return responseJson ?? throw new ApiErrorException("Failed to retrieve SQL query details.");
        }

        private async Task<string> EnsureSapSessionAsync(bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                var existing = await redis.GetAsync<string>("sessionId");
                if (!string.IsNullOrEmpty(existing))
                    return existing;
            }

            return await serviceProvider.GetRequiredService<LoginService>().SapLogin();
        }

        private static void SetSessionCookie(HttpRequestMessage request, string sessionId)
        {
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"B1SESSION={sessionId};");
        }

        private static bool IsInvalidSapSession(string json)
        {
            try
            {
                var sapResult = JsonSerializer.Deserialize<SapBaseResponse>(json);
                return sapResult?.Error?.Code == 301;
            }
            catch
            {
                return json.Contains("Invalid session", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static ByteArrayContent JsonBytesContent(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentLength = bytes.Length;
            return content;
        }

        private static async Task<TResponse?> ReadSuccessAsync<TResponse>(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default;
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
    }

    public class SapCacheResponse<T>
    {
        [JsonPropertyName("value")] public List<T>? Value { get; set; }
    }
}