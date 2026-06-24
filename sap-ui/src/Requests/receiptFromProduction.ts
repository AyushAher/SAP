import { apiDownloadGet } from '@/helpers/api/client'
import { apiListPost } from '@/helpers/api/list'
import { normalizeProductionOrderSelection } from '@/helpers/productionOrderMapper'
import type { PaginationRequest, PaginationResponse } from '@/types/api'
import type { ProductionOrderSelection } from '@/types/production'

export interface ReceiptFromProductionRequest {
  id?: number
  requestBody: string
  cardCode: string
  cardName: string
  project: string
  projectName: string
  status: string
  itemNo: string
  itemName: string
}

export async function listReceiptFromProduction(request: PaginationRequest): Promise<PaginationResponse<ReceiptFromProductionRequest[]>> {
  return apiListPost<ReceiptFromProductionRequest>('/receipt-from-production/list', request)
}

export async function getReceiptFromProductionOrderLines(id: number) {
  const { apiGet } = await import('@/helpers/api/client')
  const result = await apiGet<ProductionOrderSelection>(`/receipt-from-production/${id}/order-lines`)
  return normalizeProductionOrderSelection(result)
}

export async function saveReceiptFromProduction(orderLines: ProductionOrderSelection, id?: number) {
  const { apiPost, apiPut } = await import('@/helpers/api/client')
  if (id) return apiPut<ReceiptFromProductionRequest>(`/receipt-from-production/${id}`, orderLines)
  return apiPost<ReceiptFromProductionRequest>('/receipt-from-production', orderLines)
}

export async function deleteReceiptFromProduction(id: number) {
  const { apiDelete } = await import('@/helpers/api/client')
  return apiDelete(`/receipt-from-production/${id}`)
}

export async function downloadReceiptFromProductionPdf(id: number): Promise<void> {
  const blob = await apiDownloadGet(`/receipt-from-production/${id}/pdf`)
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `ReceiptFromProduction(${id}).pdf`
  a.click()
  URL.revokeObjectURL(url)
}
