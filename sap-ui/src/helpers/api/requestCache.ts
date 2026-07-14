const DEFAULT_TTL_MS = 5 * 60_000 // 5 minutes

type CacheEntry = {
  expiresAt: number
  promise: Promise<unknown>
}

const cache = new Map<string, CacheEntry>()

function stableSerialize(value: unknown): string {
  if (value == null) return ''
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

export function buildApiCacheKey(
  method: string,
  url: string,
  paramsOrBody?: unknown,
): string {
  return `${method.toUpperCase()}::${url}::${stableSerialize(paramsOrBody)}`
}

/** All GETs are cached except binary/download endpoints. */
export function shouldCacheApiUrl(url: string): boolean {
  const path = (url.split('?')[0] ?? url).toLowerCase()
  if (path.includes('/download')) return false
  if (path.endsWith('/pdf')) return false
  if (path.includes('/pdf?')) return false
  return true
}

/**
 * In-memory TTL cache for list POSTs and as a thin helper.
 * GET caching is owned by React Query via apiGet → queryClient.fetchQuery.
 */
export function getCachedOrFetch<T>(key: string, fetcher: () => Promise<T>, ttlMs = DEFAULT_TTL_MS): Promise<T> {
  const now = Date.now()
  const existing = cache.get(key)
  if (existing && existing.expiresAt > now)
    return existing.promise as Promise<T>

  const promise = fetcher().catch((error) => {
    cache.delete(key)
    throw error
  })

  cache.set(key, { expiresAt: now + ttlMs, promise })
  return promise
}

export function invalidateApiCache(prefix?: string): void {
  if (!prefix) {
    cache.clear()
    return
  }

  for (const key of [...cache.keys()]) {
    if (key.includes(prefix))
      cache.delete(key)
  }
}

export const API_CACHE_TTL_MS = DEFAULT_TTL_MS

export const apiGetQueryKey = (url: string, params?: Record<string, unknown>) =>
  ['api', 'GET', url, params ?? null] as const
