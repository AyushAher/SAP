import { apiListPost } from '@/helpers/api/list'
import { normalizeProductionOrder, normalizeProductionOrderSelection, normalizeProductionOrders } from '@/helpers/productionOrderMapper'
import type { PaginationRequest, PaginationResponse } from '@/types/api'
import type { ProductionOrder, ProductionOrderLine, ProductionOrderSelection } from '@/types/production'

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

export async function selectProductionOrder(absoluteEntry: string) {
  const { apiPost } = await import('@/helpers/api/client')
  const result = await apiPost<ProductionOrderSelection>(`/production-orders/select/${absoluteEntry}`)
  return normalizeProductionOrderSelection(result)
}

export async function addProductionOrderLine(absoluteEntry: string, line: ProductionOrderLine) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost(`/production-orders/${absoluteEntry}/add-line`, line)
}

export async function createProductionOrder(data: ProductionOrder, policyRequestId?: number) {
  const { apiPost } = await import('@/helpers/api/client')
  return apiPost('/production-orders', data, { policyRequestId })
}

export async function updateProductionOrder(id: number, data: ProductionOrder, policyRequestId?: number) {
  const { apiPut } = await import('@/helpers/api/client')
  return apiPut(`/production-orders/${id}`, data, { policyRequestId })
}
