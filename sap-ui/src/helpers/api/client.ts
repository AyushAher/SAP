import axiosInstance, { getApiErrorMessage } from '@/helpers/api/axiosInstance'
import { queryClient } from '@/helpers/api/queryClient'
import {
  API_CACHE_TTL_MS,
  apiGetQueryKey,
  buildApiCacheKey,
  getCachedOrFetch,
  invalidateApiCache,
  shouldCacheApiUrl,
} from '@/helpers/api/requestCache'
import type { ApiResponse } from '@/types/api'

async function fetchGet<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const { data } = await axiosInstance.get<ApiResponse<T>>(url, { params })
  if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
  return data.data as T
}

/** Clears React Query GET cache + in-memory list cache after mutations. */
export function invalidateCachedGets(): void {
  invalidateApiCache()
  void queryClient.invalidateQueries({ queryKey: ['api', 'GET'] })
}

/** POST that only computes/looks up (no persisted side effects) — do not clear GET cache. */
function shouldInvalidateAfterPost(url: string): boolean {
  const path = (url.split('?')[0] ?? url).toLowerCase()
  if (path.includes('/auth/refresh')) return false
  if (path.includes('/calculate')) return false
  if (path.includes('/lookup')) return false
  if (path.includes('/select/')) return false
  return true
}

/**
 * All GET endpoints are cached in React Query for {@link API_CACHE_TTL_MS}
 * (except download/PDF blobs).
 */
export async function apiGet<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const run = () => fetchGet<T>(url, params).catch((error) => {
    throw new Error(getApiErrorMessage(error))
  })

  if (!shouldCacheApiUrl(url))
    return run()

  return queryClient.fetchQuery({
    queryKey: apiGetQueryKey(url, params),
    queryFn: run,
    staleTime: API_CACHE_TTL_MS,
    gcTime: API_CACHE_TTL_MS * 2,
  })
}

export async function apiPost<T>(url: string, body?: unknown, params?: Record<string, unknown>): Promise<T> {
  try {
    const { data } = await axiosInstance.post<ApiResponse<T>>(url, body, { params })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    if (shouldInvalidateAfterPost(url))
      invalidateCachedGets()
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiPut<T>(url: string, body?: unknown, params?: Record<string, unknown>): Promise<T> {
  try {
    const { data } = await axiosInstance.put<ApiResponse<T>>(url, body, { params })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    invalidateCachedGets()
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiDelete<T>(url: string): Promise<T> {
  try {
    const { data } = await axiosInstance.delete<ApiResponse<T>>(url)
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    invalidateCachedGets()
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiDownload(url: string, body?: unknown): Promise<Blob> {
  const response = await axiosInstance.post(url, body, { responseType: 'blob' })
  invalidateCachedGets()
  return response.data as Blob
}

export async function apiDownloadGet(url: string): Promise<Blob> {
  // Binary payloads are never cached.
  const response = await axiosInstance.get(url, { responseType: 'blob' })
  return response.data as Blob
}

// Re-export for tests / callers that still use the list helper key builder
export { buildApiCacheKey, getCachedOrFetch }
