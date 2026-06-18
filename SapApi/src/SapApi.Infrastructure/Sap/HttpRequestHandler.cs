using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Sap;

public class HttpRequestHandler(
    HttpClient client,
    ICacheService cache,
    ISapLoginService sapLoginService) : IHttpRequestHandler
{
    private static readonly ConcurrentDictionary<string, Task<object?>> InFlightGets = new();

    public async Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"GET::{url}";
        try
        {
            if (checkCache && url.StartsWith(Constants.SapServiceLayerUrl))
            {
                var cached = await cache.GetAsync<T>(cacheKey, cancellationToken);
                if (cached is not null) return cached;
            }

            while (true)
            {
                if (InFlightGets.TryGetValue(cacheKey, out var existing))
                    return (T?)await existing;

                var task = ExecuteGetAsync<T>(url, cacheKey, checkCache, cancellationToken);
                var boxed = task.ContinueWith(static t => (object?)t.Result, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                if (InFlightGets.TryAdd(cacheKey, boxed))
                {
                    try
                    {
                        return await task;
                    }
                    finally
                    {
                        InFlightGets.TryRemove(cacheKey, out _);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GET failed for {Url}", url);
            return default;
        }
    }

    private async Task<T?> ExecuteGetAsync<T>(string url, string cacheKey, bool checkCache, CancellationToken cancellationToken)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await client.SendAsync(request, cancellationToken);
        var result = await HandleResponseAsync<T>(request, response, cancellationToken);

        if (checkCache && Constants.CachedEndpoints.ShouldCache(url) && result is not null)
            await cache.SetAsync(cacheKey, result, TimeSpan.FromHours(6), cancellationToken);

        return result;
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data, CancellationToken cancellationToken = default)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Post, url, cancellationToken);
        if (data is not null)
            request.Content = CreateJsonContent(data);

        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<TResponse>(request, response, cancellationToken);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Put, url, cancellationToken);
        request.Content = CreateJsonContent(data);
        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<TResponse>(request, response, cancellationToken);
    }

    public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Patch, url, cancellationToken);
        request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<TResponse>(request, response, cancellationToken);
    }

    public async Task PatchCachedEntityAsync<T>(string entity, int docEntry, string idProperty = "DocEntry", CancellationToken cancellationToken = default)
    {
        var singleEndpoint = $"{Constants.SapServiceLayerUrl}{Constants.SapBaseUrl}/{entity}({docEntry})";
        var updated = await GetAsync<T>(singleEndpoint, checkCache: false, cancellationToken: cancellationToken);
        if (updated == null) return;

        foreach (var endpoint in Constants.CachedEndpoints.Endpoints.Where(e => e.Contains(entity)))
        {
            var cacheKey = $"GET::{endpoint}";
            var cached = await cache.GetAsync<SapCacheResponse<T>>(cacheKey, cancellationToken);
            if (cached?.Value is null) continue;

            var index = cached.Value.FindIndex(x =>
                (int?)typeof(T).GetProperty(idProperty)?.GetValue(x) == docEntry);

            if (index >= 0) cached.Value[index] = updated;
            else cached.Value.Add(updated);

            await cache.SetAsync(cacheKey, cached, TimeSpan.FromHours(6), cancellationToken);
        }
    }

    public async Task<T?> ExecuteSqlQueryAsync<T>(string queryName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        var sqlDetails = await GetSqlQueryDetailsAsync(queryName, cancellationToken);
        if (sqlDetails == null)
            throw new ApiErrorException("SYS-01", $"SQL query details not found for query: {queryName}");

        var paramKeyValueString = string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
        var request = await BuildSapRequestAsync(HttpMethod.Post,
            $"{Constants.SapServiceLayerUrl}{Constants.SapBaseUrl}/SQLQueries('{queryName}')/List", cancellationToken);
        request.Content = CreateJsonContent(new { ParamList = paramKeyValueString });

        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<T>(request, response, cancellationToken);
    }

    private async Task<HttpRequestMessage> BuildSapRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);

        if (!url.StartsWith(Constants.SapServiceLayerUrl)) return request;

        var sessionId = await sapLoginService.GetSessionIdAsync(cancellationToken);
        if (string.IsNullOrEmpty(sessionId))
        {
            await sapLoginService.SapLoginAsync(cancellationToken);
            sessionId = await sapLoginService.GetSessionIdAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(sessionId))
            throw new ApiErrorException(BaseErrorCodes.IncorrectCredentials, "SAP session not found. Please log in again.");

        request.Headers.Add("Cookie", $"B1SESSION={sessionId};");
        return request;
    }

    private async Task<T?> HandleResponseAsync<T>(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        if (typeof(T).IsAssignableTo(typeof(SapBaseResponse)))
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var sapResult = JsonSerializer.Deserialize<SapBaseResponse>(json);
            if (sapResult?.Error?.Code == 301)
            {
                await sapLoginService.RenewSessionAsync(cancellationToken);
                var sessionId = await sapLoginService.GetSessionIdAsync(cancellationToken);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    request.Headers.Remove("Cookie");
                    request.Headers.Add("Cookie", $"B1SESSION={sessionId};");
                }

                response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            }
            return JsonSerializer.Deserialize<T>(json);
        }

        throw new ApiErrorException(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<SapQueryBaseResponse?> GetSqlQueryDetailsAsync(string queryName, CancellationToken cancellationToken)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Get,
            $"{Constants.SapServiceLayerUrl}{Constants.SapBaseUrl}/SQLQueries('{queryName}')", cancellationToken);
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ApiErrorException(await response.Content.ReadAsStringAsync(cancellationToken));
        return await response.Content.ReadFromJsonAsync<SapQueryBaseResponse>(cancellationToken);
    }

    private static ByteArrayContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = bytes.Length;
        return content;
    }
}

public class SapCacheResponse<T>
{
    [JsonPropertyName("value")] public List<T>? Value { get; set; }
}
