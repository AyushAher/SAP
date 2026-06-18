import { apiListPost } from '@/helpers/api/list'
import { normalizeProductionOrderSelection } from '@/helpers/productionOrderMapper'
import type { PaginationRequest, PaginationResponse } from '@/types/api'
import type { ProductionOrderSelection } from '@/types/production'

export interface IssueForProductionRequest {
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

export async function listIssueForProduction(request: PaginationRequest): Promise<PaginationResponse<IssueForProductionRequest[]>> {
  return apiListPost<IssueForProductionRequest>('/issue-for-production/list', request)
}

export async function getIssueForProductionOrderLines(id: number) {
  const { apiGet } = await import('@/helpers/api/client')
  const result = await apiGet<ProductionOrderSelection>(`/issue-for-production/${id}/order-lines`)
  return normalizeProductionOrderSelection(result)
}

export async function saveIssueForProduction(orderLines: ProductionOrderSelection, id?: number) {
  const { apiPost, apiPut } = await import('@/helpers/api/client')
  if (id) return apiPut<IssueForProductionRequest>(`/issue-for-production/${id}`, orderLines)
  return apiPost<IssueForProductionRequest>('/issue-for-production', orderLines)
}

export async function deleteIssueForProduction(id: number) {
  const { apiDelete } = await import('@/helpers/api/client')
  return apiDelete(`/issue-for-production/${id}`)
}
