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

    private string BuildInFlightKey(string url, int? maxPageSize) =>
        $"{companyDbAccessor.GetCompanyDbName()}::GET::{maxPageSize?.ToString() ?? "-"}::{url}";

    public async Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true, CancellationToken cancellationToken = default)
    {
        // checkCache is ignored — SAP data is never cached (no DB / Redis / durable cache).
        _ = checkCache;
        _ = setTimeout;

        try
        {
            return await GetCoalescedAsync<T>(url, null, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GET failed for {Url}", url);
            return default;
        }
    }

    public Task<T?> GetOrThrowAsync<T>(string url, CancellationToken cancellationToken = default) =>
        GetCoalescedAsync<T>(url, null, cancellationToken);

    public async Task<T?> GetPageAsync<T>(string url, int maxPageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetCoalescedAsync<T>(url, maxPageSize, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GET failed for {Url}", url);
            return default;
        }
    }

    private async Task<T?> GetCoalescedAsync<T>(string url, int? maxPageSize, CancellationToken cancellationToken)
    {
        var inFlightKey = BuildInFlightKey(url, maxPageSize);

        while (true)
        {
            if (InFlightGets.TryGetValue(inFlightKey, out var existing))
                return (T?)await existing;

            var task = ExecuteGetAsync<T>(url, maxPageSize, cancellationToken);
            // Await rather than reading Task.Result so the original exception is preserved for
            // every waiter instead of being wrapped in an AggregateException.
            var boxed = BoxAsync(task);

            if (!InFlightGets.TryAdd(inFlightKey, boxed))
            {
                // Another caller won the race; observe our task so it is not left unhandled.
                _ = boxed.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
                continue;
            }

            try
            {
                return await task;
            }
            finally
            {
                InFlightGets.TryRemove(inFlightKey, out _);
            }
        }

        static async Task<object?> BoxAsync(Task<T?> task) => await task;
    }

    private async Task<T?> ExecuteGetAsync<T>(string url, int? maxPageSize, CancellationToken cancellationToken)
    {
        var request = await BuildSapRequestAsync(HttpMethod.Get, url, cancellationToken);
        if (maxPageSize is > 0)
            request.Headers.Add("Prefer", $"odata.maxpagesize={maxPageSize}");
        var response = await client.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<T>(request, response, cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data, CancellationToken cancellationToken = default)
    {
        // Log the exact JSON that will be POSTed (after caller payload build / Series resolution).
        // Do not log cookies/session — those are attached later in BuildSapRequestAsync.
        var companyDb = companyDbAccessor.GetCompanyDb()?.ToString() ?? "(none)";
        if (data is not null)
        {
            Log.Information(
                "SAP POST {CompanyDb} {Url} body: {Body}",
                companyDb,
                url,
                JsonSerializer.Serialize(data));
        }
        else
        {
            Log.Information("SAP POST {CompanyDb} {Url} body: null", companyDb, url);
        }

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

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (typeof(T).IsAssignableTo(typeof(SapBaseResponse)))
        {
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

                json = await response.Content.ReadAsStringAsync(cancellationToken);
                sapResult = JsonSerializer.Deserialize<SapBaseResponse>(json);
            }

            // Never treat SAP error payloads as successful typed responses — callers would
            // wrap them in ApiResponse.Ok and the UI would redirect as if the document existed.
            throw new ApiErrorException(
                BaseErrorCodes.ValidationFailed,
                SapErrorFormatter.Format(sapResult, json, response.StatusCode));
        }

        throw new ApiErrorException(
            BaseErrorCodes.ValidationFailed,
            SapErrorFormatter.TryExtractMessage(json) ?? $"SAP Service Layer request failed ({(int)response.StatusCode}).");
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
