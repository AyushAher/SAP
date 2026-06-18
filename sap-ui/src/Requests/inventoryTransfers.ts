import { apiListPost } from '@/helpers/api/list'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface InventoryTransfer {
  DocEntry?: number
  DocDate?: string
  FromWarehouse?: string
  ToWarehouse?: string
  CardCode?: string
  CardName?: string
  [key: string]: unknown
}

export async function listInventoryTransfers(request: PaginationRequest): Promise<PaginationResponse<InventoryTransfer[]>> {
  return apiListPost<InventoryTransfer>('/inventory-transfers/list', request)
}

export async function getInventoryTransfer(id: string) {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<InventoryTransfer>(`/inventory-transfers/${id}`)
}

export async function createInventoryTransfer(data: InventoryTransfer, policyRequestId?: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost('/inventory-transfers', data, { policyRequestId })
}

export async function updateInventoryTransfer(docEntry: string, data: InventoryTransfer, policyRequestId?: number) {
  const { apiPut } = await import('@/helpers/api/client')
  return apiPut(`/inventory-transfers/${docEntry}`, data, { policyRequestId })
}

export async function closeInventoryTransfer(docEntry: string) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost(`/inventory-transfers/${docEntry}/close`)
}

export async function cancelInventoryTransfer(docEntry: string) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost(`/inventory-transfers/${docEntry}/cancel`)
}
