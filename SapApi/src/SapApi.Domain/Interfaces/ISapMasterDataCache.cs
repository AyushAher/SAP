namespace SapApi.Domain.Interfaces;

/// <summary>
/// Durable, tenant-aware cache for relatively static SAP master data (vendors, items, warehouses,
/// projects, tax codes, etc.). Backed by the app's distributed cache (Redis in production, an
/// in-memory fallback otherwise — see DependencyInjection.AddDistributedCache), so entries are shared
/// across API instances and survive individual request scopes.
/// </summary>
public interface ISapMasterDataCache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/> if present and unexpired; otherwise invokes
    /// <paramref name="factory"/>, caches a non-null result for <paramref name="ttl"/>, and returns it.
    /// A null/default result from <paramref name="factory"/> is never cached, so a transient SAP failure
    /// doesn't get "stuck" as a false negative for the remainder of the TTL.
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default);
}
