import { apiListPost } from '@/helpers/api/list'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface PurchaseOrder {
  DocEntry?: number
  DocNum?: number
  DocDate?: string
  BPLId?: number
  /** SAP wire name for branch id — kept for backward compatibility with older API payloads. */
  BPL_IDAssignedToInvoice?: number
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

export function getPurchaseOrderBranchId(order: Pick<PurchaseOrder, 'BPLId' | 'BPL_IDAssignedToInvoice'>): number | undefined {
  return order.BPLId ?? order.BPL_IDAssignedToInvoice
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
  /** True when the server stopped early and the sync must be continued. */
  hasMore?: boolean
  /** Resume cursor for the next batch. */
  lastDocEntry?: number | null
  /** Idle | Running | Succeeded | Failed */
  status?: string
  hangfireJobId?: string | null
  startedAtUtc?: string | null
}

export interface PurchaseOrderFullSyncJobResult {
  jobId?: string | null
  status: string
  message?: string
  alreadyRunning?: boolean
}

export interface SyncProgress {
  batches: number
  upsertedCount: number
  addedCount: number
  updatedCount: number
  lastDocEntry?: number | null
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

/** Server caps work per call, so a single batch must stay well under the proxy read timeout. */
const SYNC_BATCH_TIMEOUT_MS = 2 * 60_000

/** Guards against an unexpected hasMore loop that never terminates. */
const MAX_SYNC_BATCHES = 500

async function postSync(url: string, timeoutMs = SYNC_BATCH_TIMEOUT_MS): Promise<PurchaseOrderSyncResult> {
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

/**
 * Runs a resumable sync to completion. The server returns after a bounded batch so no single
 * request can hit the reverse-proxy read timeout (which surfaced as a 504); this keeps calling
 * with the resume cursor until the server reports there is nothing left.
 */
async function runResumableSync(
  path: string,
  onProgress?: (progress: SyncProgress) => void,
): Promise<PurchaseOrderSyncResult> {
  const totals: SyncProgress = { batches: 0, upsertedCount: 0, addedCount: 0, updatedCount: 0 }
  let result = await postSync(path)

  for (;;) {
    totals.batches += 1
    totals.upsertedCount += result.upsertedCount ?? 0
    totals.addedCount += result.addedCount ?? 0
    totals.updatedCount += result.updatedCount ?? 0
    totals.lastDocEntry = result.lastDocEntry ?? totals.lastDocEntry
    onProgress?.({ ...totals })

    if (!result.hasMore || totals.batches >= MAX_SYNC_BATCHES) break
    if (result.lastDocEntry == null) break

    result = await postSync(`${path}?afterDocEntry=${result.lastDocEntry}`)
  }

  return {
    ...result,
    upsertedCount: totals.upsertedCount,
    addedCount: totals.addedCount,
    updatedCount: totals.updatedCount,
    hasMore: false,
    message: totals.batches > 1
      ? `Synced ${totals.upsertedCount} purchase order(s) (${totals.addedCount} added, ${totals.updatedCount} updated).`
      : result.message,
  }
}

/** Incremental: add POs that exist in SAP but not yet locally (DocEntry &gt; local max). */
export function syncNewPurchaseOrdersFromSap(onProgress?: (progress: SyncProgress) => void) {
  return runResumableSync('/purchase-orders/sync', onProgress)
}

/** Full re-import of all POs from SAP (browser-driven resumable loop — prefer enqueueFullPurchaseOrderSyncJob). */
export function syncAllPurchaseOrdersFromSap(onProgress?: (progress: SyncProgress) => void) {
  return runResumableSync('/purchase-orders/sync/full', onProgress)
}

/** Enqueue Hangfire full sync for the current company. Poll getPurchaseOrderSyncStatus while Running. */
export async function enqueueFullPurchaseOrderSyncJob(): Promise<PurchaseOrderFullSyncJobResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: PurchaseOrderFullSyncJobResult
    }>('/purchase-orders/sync/jobs/full')
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Failed to start sync job')
    return data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
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
