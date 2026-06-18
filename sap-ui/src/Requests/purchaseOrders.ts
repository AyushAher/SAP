import { apiListPost } from '@/helpers/api/list'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface PurchaseOrder {
  DocEntry?: number
  DocNum?: number
  CardCode?: string
  CardName?: string
  Project?: string
  DocTotal?: number
  DocumentStatus?: string
  VatSum?: number
  DocumentLines?: unknown[]
  [key: string]: unknown
}

export async function listPurchaseOrders(request: PaginationRequest): Promise<PaginationResponse<PurchaseOrder[]>> {
  return apiListPost<PurchaseOrder>('/purchase-orders/list', request)
}

export async function getPurchaseOrder(id: string | number) {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<PurchaseOrder>(`/purchase-orders/${id}`)
}

export async function createPurchaseOrder(data: PurchaseOrder, policyRequestId?: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost<PurchaseOrder>('/purchase-orders', data, { policyRequestId })
}

export async function updatePurchaseOrder(docEntry: number, data: PurchaseOrder, policyRequestId?: number) {
  const { apiPut } = await import('@/helpers/api/client')
  return apiPut<PurchaseOrder>(`/purchase-orders/${docEntry}`, data, { policyRequestId })
}
