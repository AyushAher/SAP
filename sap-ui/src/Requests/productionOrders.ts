import { apiListPost } from '@/helpers/api/list'
import {
  normalizeProductionOrder,
  normalizeProductionOrderSelection,
  normalizeProductionOrders,
  toProductionOrderPayload,
} from '@/helpers/productionOrderMapper'
import type { PaginationRequest, PaginationResponse } from '@/types/api'
import type {
  ProductionOrder,
  ProductionOrderAddLineResult,
  ProductionOrderLine,
  ProductionOrderSelection,
} from '@/types/production'

export type { ProductionOrder }

export async function listProductionOrders(request: PaginationRequest): Promise<PaginationResponse<ProductionOrder[]>> {
  const response = await apiListPost<ProductionOrder>('/production-orders/list', request)
  return { ...response, data: normalizeProductionOrders(response.data) }
}

export async function getProductionOrders(request: PaginationRequest): Promise<{ value?: ProductionOrder[] }> {
  const response = await listProductionOrders(request)
  return { value: response.data ?? [] }
}

export async function getProductionOrder(id: string | number) {
  const { apiGet } = await import('@/helpers/api/client')
  const order = await apiGet<ProductionOrder>(`/production-orders/${id}`)
  return normalizeProductionOrder(order)
}

export async function getProductionOrderLines(id: string | number) {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<{ Value?: ProductionOrderLine[]; value?: ProductionOrderLine[] }>(`/production-orders/${id}/lines`)
}

export interface ProductionOrderSyncResult {
  companyDb: string
  upsertedCount: number
  pageCount: number
  syncedAtUtc: string
  message: string
  mode?: string
  addedCount?: number
  updatedCount?: number
  absoluteEntry?: number | null
  /** True when the server stopped early and the sync must be continued. */
  hasMore?: boolean
  /** Resume cursor for the next batch. */
  lastAbsoluteEntry?: number | null
  /** Idle | Running | Succeeded | Failed */
  status?: string
  hangfireJobId?: string | null
  startedAtUtc?: string | null
}

export interface ProductionOrderFullSyncJobResult {
  jobId?: string | null
  status: string
  message?: string
  alreadyRunning?: boolean
}

/** The server caps work per call, so a single batch stays well under the proxy read timeout. */
const SYNC_BATCH_TIMEOUT_MS = 2 * 60_000

async function postSync(url: string, timeoutMs = SYNC_BATCH_TIMEOUT_MS): Promise<ProductionOrderSyncResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  const { invalidateCachedGets } = await import('@/helpers/api/client')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: ProductionOrderSyncResult
    }>(url, undefined, { timeout: timeoutMs })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Sync failed')
    invalidateCachedGets()
    return data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error), { cause: error })
  }
}

/** Refresh a single production order row from SAP into the local mirror. */
export function syncProductionOrderFromSap(absoluteEntry: number) {
  return postSync(`/production-orders/${absoluteEntry}/sync`, 60_000)
}

/** Enqueue the Hangfire sync for the current company. Poll getProductionOrderSyncStatus while Running. */
export async function enqueueFullProductionOrderSyncJob(): Promise<ProductionOrderFullSyncJobResult> {
  const axiosInstance = (await import('@/helpers/api/axiosInstance')).default
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  try {
    const { data } = await axiosInstance.post<{
      success: boolean
      message?: string
      errorCode?: string
      data: ProductionOrderFullSyncJobResult
    }>('/production-orders/sync/jobs/full')
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Failed to start sync job')
    return data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error), { cause: error })
  }
}

export async function getProductionOrderSyncStatus() {
  const { apiGet } = await import('@/helpers/api/client')
  return apiGet<ProductionOrderSyncResult | null>('/production-orders/sync-status')
}

export async function selectProductionOrder(absoluteEntry: string) {
  const { apiPost } = await import('@/helpers/api/client')
  const result = await apiPost<ProductionOrderSelection>(`/production-orders/select/${absoluteEntry}`)
  return normalizeProductionOrderSelection(result)
}

export async function addProductionOrderLine(
  absoluteEntry: string,
  line: ProductionOrderLine,
): Promise<ProductionOrderAddLineResult> {
  const { apiPost } = await import('@/helpers/api/client')
  const { normalizeProductionOrder, normalizeProductionOrderLine } = await import('@/helpers/productionOrderMapper')
  const result = await apiPost<{
    AddedLine?: ProductionOrderLine
    addedLine?: ProductionOrderLine
    ProductionOrder?: ProductionOrder
    productionOrder?: ProductionOrder
  }>(`/production-orders/${absoluteEntry}/add-line`, line)

  const addedRaw = result?.AddedLine ?? result?.addedLine
  const orderRaw = result?.ProductionOrder ?? result?.productionOrder

  return {
    AddedLine: normalizeProductionOrderLine((addedRaw ?? line) as ProductionOrderLine),
    ProductionOrder: orderRaw ? normalizeProductionOrder(orderRaw) : undefined,
  }
}

export interface ProductionOrderWriteResult {
  AbsoluteEntry?: number
  DocumentNumber?: number
  /** Set when create/update is deferred pending approval (not yet in SAP). */
  pendingApproval?: boolean
  pendingApprovalRequestId?: number
}

export async function createProductionOrder(data: ProductionOrder, policyRequestId?: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost<ProductionOrderWriteResult>(
    '/production-orders',
    toProductionOrderPayload(data),
    { policyRequestId },
  )
}

export async function updateProductionOrder(id: number, data: ProductionOrder, policyRequestId?: number) {
  const { apiPut } = await import('@/helpers/api/client')
  return apiPut<ProductionOrderWriteResult>(
    `/production-orders/${id}`,
    toProductionOrderPayload(data),
    { policyRequestId },
  )
}

/**
 * A failed download answers with the standard error envelope, but inside a blob because the
 * request asked for binary — unwrap it so the caller can show the server's message.
 */
async function readDownloadErrorMessage(error: unknown): Promise<string> {
  const { getApiErrorMessage } = await import('@/helpers/api/axiosInstance')
  const body = (error as { response?: { data?: unknown } })?.response?.data
  if (body instanceof Blob) {
    try {
      const parsed = JSON.parse(await readBlobText(body)) as { message?: string; errors?: string[] }
      const message = parsed.message?.trim() || parsed.errors?.filter(Boolean).join(' ')
      if (message) return message
    } catch {
      // Not a JSON envelope; fall back to the generic axios message.
    }
  }
  return getApiErrorMessage(error)
}

/** Blob.text() is not available on every runtime, so fall back to FileReader. */
function readBlobText(blob: Blob): Promise<string> {
  if (typeof blob.text === 'function') return blob.text()
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(String(reader.result ?? ''))
    reader.onerror = () => reject(reader.error ?? new Error('Could not read the response.'))
    reader.readAsText(blob)
  })
}

/** Downloads the printed production order. Throws with a readable message when it fails. */
export async function downloadProductionOrderPdf(
  absoluteEntry: number,
  documentNumber?: number | null,
): Promise<void> {
  const { apiDownloadGet } = await import('@/helpers/api/client')
  let blob: Blob
  try {
    blob = await apiDownloadGet(`/production-orders/${absoluteEntry}/pdf`)
  } catch (error) {
    throw new Error(await readDownloadErrorMessage(error), { cause: error })
  }

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `ProductionOrder(${documentNumber ?? absoluteEntry}).pdf`
  a.click()
  URL.revokeObjectURL(url)
}
