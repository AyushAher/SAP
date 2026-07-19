import { apiGet, apiPost } from '@/helpers/api/client'
import { apiListPost } from '@/helpers/api/list'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface UserApproval {
  userId: number
  approvalStatus: string
  priority: number
  comment?: string
  actionDate?: string
  user?: { fullName?: string; userName?: string }
}

export interface ApprovalRequest {
  id: number
  documentType: string
  action?: string
  overallStatus: string
  requestBody?: string
  supportingData?: string
  failureReason?: string
  sapResponseDocNum?: string
  sapResponseDocEntry?: string
  createdAt: string
  isLastApproval?: boolean
  requesterUser?: { fullName?: string; userName?: string }
  userApprovals?: UserApproval[]
  policy?: { documentType?: string }
}

export interface ApprovalTimelineItem {
  approverName?: string
  actionDate?: string
  comment?: string
  status?: string
}

export interface StageWisePaymentSummaryItem {
  requestId?: string
  paymentStage?: string
  netBasicAmount: number
  tdsAmount: number
  gstAmount: number
  grossAmount: number
  status?: string
  isTotalRow?: boolean
}

export interface ApprovalPaymentContext {
  vendorDisplay?: string
  poDetails?: string
  projectName?: string
  bankAccount?: string
  branch?: string
  transferAmount?: number
  utrNo?: string
  utrDate?: string
  previousApprovals: ApprovalTimelineItem[]
  stageWisePayments: StageWisePaymentSummaryItem[]
  paymentTerms: Array<{ id?: number; desc?: string; type?: string }>
}

export async function listPendingApprovals(request: PaginationRequest): Promise<PaginationResponse<ApprovalRequest[]>> {
  return apiListPost<ApprovalRequest>('/approvals/pending/list', request)
}

export async function listMyApprovalRequests(request: PaginationRequest): Promise<PaginationResponse<ApprovalRequest[]>> {
  return apiListPost<ApprovalRequest>('/approvals/my-requests/list', request)
}

export async function getApprovalRequest(id: number) {
  return apiGet<ApprovalRequest>(`/approvals/${id}`)
}

export async function getApprovalPaymentContext(id: number) {
  return apiGet<ApprovalPaymentContext>(`/approvals/${id}/payment-context`)
}

export async function approveRequest(id: number, payload: { comment?: string; utrNo?: string; utrDate?: string }) {
  return apiPost(`/approvals/${id}/approve`, { action: 'Approve', ...payload })
}

export async function rejectRequest(id: number, comment?: string) {
  return apiPost(`/approvals/${id}/reject`, { action: 'Reject', comment })
}

export interface BulkActionResultItem {
  id: number
  error?: string
}

export async function bulkApprove(ids: number[]) {
  return apiPost<BulkActionResultItem[]>('/approvals/bulk-approve', ids)
}

export async function bulkReject(ids: number[], comment?: string) {
  return apiPost<BulkActionResultItem[] | null>('/approvals/bulk-reject', { requestIds: ids, comment })
}
