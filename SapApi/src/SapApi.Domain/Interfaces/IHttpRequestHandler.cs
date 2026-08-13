namespace SapApi.Domain.Interfaces;

public interface IHttpRequestHandler
{
    /// <param name="checkCache">
    /// Ignored here — this handler itself never caches responses (it only coalesces concurrent
    /// identical in-flight requests). Callers that want a durable cache (e.g. master data lookups)
    /// should wrap this call with <see cref="ISapMasterDataCache"/> instead.
    /// </param>
    Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetAsync{T}"/> but propagates SAP/transport failures instead of returning
    /// <c>default</c>. Use this where a swallowed error would be reported to the user as success
    /// (for example the purchase order sync, which would otherwise silently skip records).
    /// </summary>
    Task<T?> GetOrThrowAsync<T>(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetAsync{T}"/> but asks SAP for a specific collection page size
    /// (<c>Prefer: odata.maxpagesize</c>). Service Layer caps <c>$top</c> at its own page size
    /// (20 by default), so callers that must walk a filtered collection use this to keep the
    /// number of round trips down.
    /// </summary>
    Task<T?> GetPageAsync<T>(string url, int maxPageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetPageAsync{T}"/> but lets the SAP failure surface. Use it where a
    /// swallowed error would be reported to the user as a successful run that did nothing.
    /// </summary>
    Task<T?> GetPageOrThrowAsync<T>(string url, int maxPageSize, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data, CancellationToken cancellationToken = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default);
    Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default);
    Task<T?> ExecuteSqlQueryAsync<T>(string queryName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
