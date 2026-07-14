import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import {
  buildApiCacheKey,
  getCachedOrFetch,
  invalidateApiCache,
  shouldCacheApiUrl,
} from './requestCache'

describe('requestCache', () => {
  beforeEach(() => {
    invalidateApiCache()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    invalidateApiCache()
  })

  it('reuses the same promise within the TTL', async () => {
    const fetcher = vi.fn(async () => ({ ok: true }))
    const key = buildApiCacheKey('GET', '/purchase-orders', { page: 1 })

    const first = getCachedOrFetch(key, fetcher)
    const second = getCachedOrFetch(key, fetcher)

    await expect(first).resolves.toEqual({ ok: true })
    await expect(second).resolves.toEqual({ ok: true })
    expect(fetcher).toHaveBeenCalledTimes(1)
  })

  it('refetches after TTL expires', async () => {
    const fetcher = vi.fn(async () => Math.random())
    const key = buildApiCacheKey('GET', '/items')

    await getCachedOrFetch(key, fetcher, 5 * 60_000)
    expect(fetcher).toHaveBeenCalledTimes(1)

    vi.advanceTimersByTime(5 * 60_000 + 1)
    await getCachedOrFetch(key, fetcher, 5 * 60_000)
    expect(fetcher).toHaveBeenCalledTimes(2)
  })

  it('caches all GETs except download/pdf endpoints', () => {
    expect(shouldCacheApiUrl('/auth/branches')).toBe(true)
    expect(shouldCacheApiUrl('/purchase-orders/12')).toBe(true)
    expect(shouldCacheApiUrl('/stage-wise-payments/1/pdf')).toBe(false)
    expect(shouldCacheApiUrl('/files/download')).toBe(false)
  })
})
