import { apiListPost } from '@/helpers/api/list'
import type { Filter, PaginationRequest, PaginationResponse } from '@/types/api'

export interface PurchaseRequest {
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

export function getPurchaseRequestBranchId(order: Pick<PurchaseRequest, 'BPLId' | 'BPL_IDAssignedToInvoice'>): number | undefined {
  return order.BPLId ?? order.BPL_IDAssignedToInvoice
}

export interface PurchaseRequestSyncResult {
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

export interface PurchaseRequestFullSyncJobResult {
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

export async function listPurchaseRequests(request: PaginationRequest): Promise<PaginationResponse<PurchaseRequest[]>> {
  return apiListPost<PurchaseRequest>('/purchase-requests/list', request)
}

export async function getPurchaseRequest(id: string | number) {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<PurchaseRequest>(`/purchase-requests/${id}`)
}

export async function createPurchaseRequest(data: PurchaseRequest, policyRequestId?: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost<PurchaseRequest>('/purchase-requests', data, { policyRequestId })
}

export async function updatePurchaseRequest(docEntry: number, data: PurchaseRequest, policyRequestId?: number) {
  const { apiPut } = await import('@/helpers/api/client')
  return apiPut<PurchaseRequest>(`/purchase-requests/${docEntry}`, data, { policyRequestId })
}

/** Server caps work per call, so a single batch must stay well under the proxy read timeout. */
const SYNC_BATCH_TIMEOUT_MS = 2 * 60_000

/** Guards against an unexpected hasMore loop that never terminates. */
const MAX_SYNC_BATCHES = 500

async function postSync(url: string, timeoutMs = SYNC_BATCH_TIMEOUT_MS): Promise<PurchaseRequestSyncResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  const { invalidateCachedGets } = await import('@/helpers/api/client')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: PurchaseRequestSyncResult
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
): Promise<PurchaseRequestSyncResult> {
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
      ? `Synced ${totals.upsertedCount} purchase request(s) (${totals.addedCount} added, ${totals.updatedCount} updated).`
      : result.message,
  }
}

/** Incremental: add POs that exist in SAP but not yet locally (DocEntry &gt; local max). */
export function syncNewPurchaseRequestsFromSap(onProgress?: (progress: SyncProgress) => void) {
  return runResumableSync('/purchase-requests/sync', onProgress)
}

/** Full re-import of all POs from SAP (browser-driven resumable loop — prefer enqueueFullPurchaseRequestSyncJob). */
export function syncAllPurchaseRequestsFromSap(onProgress?: (progress: SyncProgress) => void) {
  return runResumableSync('/purchase-requests/sync/full', onProgress)
}

/** Enqueue Hangfire full sync for the current company. Poll getPurchaseRequestSyncStatus while Running. */
export async function enqueueFullPurchaseRequestSyncJob(): Promise<PurchaseRequestFullSyncJobResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: PurchaseRequestFullSyncJobResult
    }>('/purchase-requests/sync/jobs/full')
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Failed to start sync job')
    return data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

/** Refresh a single PO row from SAP. */
export function syncPurchaseRequestFromSap(docEntry: number) {
  return postSync(`/purchase-requests/${docEntry}/sync`, 60_000)
}

/** @deprecated Prefer syncNewPurchaseRequestsFromSap */
export const syncPurchaseRequestsFromSap = syncNewPurchaseRequestsFromSap

export async function getPurchaseRequestSyncStatus() {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<PurchaseRequestSyncResult | null>('/purchase-requests/sync-status')
}

export async function cancelPurchaseRequest(docEntry: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost<PurchaseRequest>(`/purchase-requests/${docEntry}/cancel`)
}

export async function downloadPurchaseRequestReportPdf(filters: Filter[]): Promise<Blob> {
  const { apiDownload } = await import('@/helpers/api/client')
  return apiDownload('/purchase-requests/report/pdf', filters)
}

export async function downloadPurchaseRequestPdf(docEntry: number): Promise<void> {
  const { apiDownloadGet } = await import('@/helpers/api/client')
  const blob = await apiDownloadGet(`/purchase-requests/${docEntry}/pdf`)
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `PurchaseRequisition(${docEntry}).pdf`
  a.click()
  URL.revokeObjectURL(url)
}
