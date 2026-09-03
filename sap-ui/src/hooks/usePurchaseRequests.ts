import { useCallback } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getPurchaseRequest,
  listPurchaseRequests,
  type PurchaseRequest,
} from '@/Requests/purchaseRequests'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export const purchaseRequestKeys = {
  all: ['purchase-requests'] as const,
  lists: () => [...purchaseRequestKeys.all, 'list'] as const,
  list: (request: PaginationRequest) =>
    [...purchaseRequestKeys.lists(), request] as const,
  details: () => [...purchaseRequestKeys.all, 'detail'] as const,
  detail: (id: string | number) =>
    [...purchaseRequestKeys.details(), String(id)] as const,
}

/** Always fetch fresh from API — POs are DB-backed; no client TTL cache. */
export function usePurchaseRequest(id?: string | number) {
  const enabled = id != null && String(id).length > 0
  return useQuery<PurchaseRequest>({
    queryKey: purchaseRequestKeys.detail(id ?? ''),
    queryFn: () => getPurchaseRequest(id!),
    enabled,
    staleTime: 0,
    gcTime: 0,
  })
}

/** DataTable-compatible fetcher — always hits API (DB-backed, no client TTL). */
export function usePurchaseRequestListFetcher() {
  return useCallback(
    (request: PaginationRequest): Promise<PaginationResponse<PurchaseRequest[]>> =>
      listPurchaseRequests(request),
    [],
  )
}

export function useInvalidatePurchaseRequests() {
  const queryClient = useQueryClient()
  return (id?: string | number) => {
    const tasks = [
      queryClient.invalidateQueries({ queryKey: purchaseRequestKeys.lists() }),
    ]
    if (id != null)
      tasks.push(queryClient.invalidateQueries({ queryKey: purchaseRequestKeys.detail(id) }))
    return Promise.all(tasks)
  }
}
