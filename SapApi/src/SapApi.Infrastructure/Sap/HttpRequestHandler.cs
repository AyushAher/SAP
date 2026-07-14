using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Sap;

public class HttpRequestHandler(
    HttpClient client,
    ISapLoginService sapLoginService,
    ICurrentCompanyDbAccessor companyDbAccessor) : IHttpRequestHandler
{
    /// <summary>
    /// Coalesces concurrent identical GETs within the same process. Not a durable cache —
    /// completed requests are not retained; each new request hits SAP Service Layer.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Task<object?>> InFlightGets = new();

    private string BuildInFlightKey(string url) => $"{companyDbAccessor.GetCompanyDbName()}::GET::{url}";

    public async Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true, CancellationToken cancellationToken = default)
    {
        // checkCache is ignored — SAP data is never cached (no DB / Redis / durable cache).
        _ = checkCache;
        _ = setTimeout;

        var inFlightKey = BuildInFlightKey(url);
        try
        {
            while (true)
            {
                if (InFlightGets.TryGetValue(inFlightKey, out var existing))
                    return (T?)await existing;

                var task = ExecuteGetAsync<T>(url, cancellationToken);
                var boxed = task.ContinueWith(
                    static t => (object?)t.Result,
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                if (!InFlightGets.TryAdd(inFlightKey, boxed))
                    continue;

                try
                {
                    return await task;
                }
                finally
                {
                    InFlightGets.TryRemove(inFlightKey, out _);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GET failed for {Url}", url);
            return default;
        }
    }

    private async Task<T?> ExecuteGetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<T>(request, response, cancellationToken);
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
