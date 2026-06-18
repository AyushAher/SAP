namespace SapApi.Domain.Interfaces;

public interface IHttpRequestHandler
{
    Task<T?> GetAsync<T>(string url, bool setTimeout = true, bool checkCache = true, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest? data, CancellationToken cancellationToken = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default);
    Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default);
    Task PatchCachedEntityAsync<T>(string entity, int docEntry, string idProperty = "DocEntry", CancellationToken cancellationToken = default);
    Task<T?> ExecuteSqlQueryAsync<T>(string queryName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
