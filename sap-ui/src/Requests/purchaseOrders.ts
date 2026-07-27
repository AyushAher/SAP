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
  /** Set when create/update is deferred pending approval (not yet in SAP). */
  pendingApproval?: boolean
  pendingApprovalRequestId?: number
  [key: string]: unknown
}

export interface PurchaseOrderSyncResult {
  companyDb: string
  upsertedCount: number
  pageCount: number
  syncedAtUtc: string
  message: string
  mode?: string
  addedCount?: number
  updatedCount?: number
  docEntry?: number | null
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

async function postSync(url: string, timeoutMs = 10 * 60_000): Promise<PurchaseOrderSyncResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  const { invalidateCachedGets } = await import('@/helpers/api/client')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: PurchaseOrderSyncResult
    }>(url, undefined, { timeout: timeoutMs })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Sync failed')
    invalidateCachedGets()
    return data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

/** Incremental: add POs that exist in SAP but not yet locally (DocEntry &gt; local max). */
export function syncNewPurchaseOrdersFromSap() {
  return postSync('/purchase-orders/sync')
}

/** Full re-import of all POs from SAP. */
export function syncAllPurchaseOrdersFromSap() {
  return postSync('/purchase-orders/sync/full')
}

/** Refresh a single PO row from SAP. */
export function syncPurchaseOrderFromSap(docEntry: number) {
  return postSync(`/purchase-orders/${docEntry}/sync`, 60_000)
}

/** @deprecated Prefer syncNewPurchaseOrdersFromSap */
export const syncPurchaseOrdersFromSap = syncNewPurchaseOrdersFromSap

export async function getPurchaseOrderSyncStatus() {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<PurchaseOrderSyncResult | null>('/purchase-orders/sync-status')
}
