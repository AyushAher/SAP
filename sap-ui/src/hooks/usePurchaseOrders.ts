import { useCallback } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getPurchaseOrder,
  listPurchaseOrders,
  type PurchaseOrder,
} from '@/Requests/purchaseOrders'
import { API_CACHE_TTL_MS } from '@/helpers/api/requestCache'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export const purchaseOrderKeys = {
  all: ['purchase-orders'] as const,
  lists: () => [...purchaseOrderKeys.all, 'list'] as const,
  list: (request: PaginationRequest) =>
    [...purchaseOrderKeys.lists(), request] as const,
  details: () => [...purchaseOrderKeys.all, 'detail'] as const,
  detail: (id: string | number) =>
    [...purchaseOrderKeys.details(), String(id)] as const,
}

export function usePurchaseOrder(id?: string | number) {
  const enabled = id != null && String(id).length > 0
  return useQuery<PurchaseOrder>({
    queryKey: purchaseOrderKeys.detail(id ?? ''),
    queryFn: () => getPurchaseOrder(id!),
    enabled,
    staleTime: API_CACHE_TTL_MS,
  })
}

/** DataTable-compatible fetcher backed by React Query (5 min stale). */
export function usePurchaseOrderListFetcher() {
  const queryClient = useQueryClient()
  return useCallback(
    (request: PaginationRequest): Promise<PaginationResponse<PurchaseOrder[]>> =>
      queryClient.fetchQuery({
        queryKey: purchaseOrderKeys.list(request),
        queryFn: () => listPurchaseOrders(request),
        staleTime: API_CACHE_TTL_MS,
      }),
    [queryClient],
  )
}

export function useInvalidatePurchaseOrders() {
  const queryClient = useQueryClient()
  return (id?: string | number) => {
    const tasks = [
      queryClient.invalidateQueries({ queryKey: purchaseOrderKeys.lists() }),
    ]
    if (id != null)
      tasks.push(queryClient.invalidateQueries({ queryKey: purchaseOrderKeys.detail(id) }))
    return Promise.all(tasks)
  }
}
