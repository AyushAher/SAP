import axiosInstance from '@/helpers/api/axiosInstance'
import { getApiErrorMessage } from '@/helpers/api/axiosInstance'
import { createDefaultPaginationRequest } from '@/helpers/api/pagination'
import {
  buildApiCacheKey,
  getCachedOrFetch,
  shouldCacheApiUrl,
} from '@/helpers/api/requestCache'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export async function apiListPost<T>(
  url: string,
  request?: PaginationRequest,
): Promise<PaginationResponse<T[]>> {
  const body = createDefaultPaginationRequest(request)

  const run = async () => {
    try {
      const { data } = await axiosInstance.post<PaginationResponse<T[]>>(url, body)
      if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
      return data
    } catch (error) {
      throw new Error(getApiErrorMessage(error))
    }
  }

  if (!shouldCacheApiUrl(url))
    return run()

  return getCachedOrFetch(buildApiCacheKey('LIST', url, body), run)
}
