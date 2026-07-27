import { useCallback } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getPurchaseOrder,
  listPurchaseOrders,
  type PurchaseOrder,
} from '@/Requests/purchaseOrders'
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

/** Always fetch fresh from API — POs are DB-backed; no client TTL cache. */
export function usePurchaseOrder(id?: string | number) {
  const enabled = id != null && String(id).length > 0
  return useQuery<PurchaseOrder>({
    queryKey: purchaseOrderKeys.detail(id ?? ''),
    queryFn: () => getPurchaseOrder(id!),
    enabled,
    staleTime: 0,
    gcTime: 0,
  })
}

/** DataTable-compatible fetcher — always hits API (DB-backed, no client TTL). */
export function usePurchaseOrderListFetcher() {
  return useCallback(
    (request: PaginationRequest): Promise<PaginationResponse<PurchaseOrder[]>> =>
      listPurchaseOrders(request),
    [],
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
