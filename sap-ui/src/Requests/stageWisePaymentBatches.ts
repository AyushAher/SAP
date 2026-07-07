import { apiGet, apiPost, apiDelete, apiDownloadGet } from '@/helpers/api/client'
import type { StageWisePaymentPageData } from '@/Requests/stageWisePayments'

export interface StageWisePaymentBatchLine {
  id?: number
  apInvoiceDocEntry?: string
  apInvoiceLabel?: string
  paymentTermsTypes: number[]
  paymentTermLabels?: string[]
  bank?: string
  wtCode?: string
  amount: number
  balanceDue: number
  payable: number
  notes?: string
}

export interface StageWisePaymentBatch {
  id: number
  poDocEntry: number
  docNumber?: number
  stageWisePaymentId?: number
  downPaymentStageWisePaymentId?: number
  approvalRequestId?: string
  approvalRequestIdNumeric?: number
  status: string
  readOnly: boolean
  canCancel: boolean
  canDelete: boolean
  canApprove: boolean
  canReject: boolean
  isLastApproval: boolean
  wtCode?: string
  modeOfPayment?: string
  modeOfPaymentLabel?: string
  account?: string
  accountLabel?: string
  journalRemark?: string
  referenceNo?: string
  postingDate?: string
  paymentDate?: string
  lines: StageWisePaymentBatchLine[]
}

export interface CalculateBatchLineResult {
  balanceDue: number
  payable: number
}

export interface CreateBatchLinePayload {
  apInvoiceDocEntry?: string
  paymentTermsTypes: number[]
  bank?: string
  wtCode?: string
  amount: number
  notes?: string
}

export async function getBatchPageData(poDocEntry: number): Promise<StageWisePaymentPageData> {
  return apiGet<StageWisePaymentPageData>(`/stage-wise-payment-batches/page-data/${poDocEntry}`)
}

export async function calculateBatchLine(payload: {
  poDocEntry: number
  apInvoiceDocEntry?: string
  paymentTermsTypes: number[]
  excludeBatchId?: number
}): Promise<CalculateBatchLineResult> {
  return apiPost<CalculateBatchLineResult>('/stage-wise-payment-batches/calculate-line', payload)
}

export async function createStageWisePaymentBatch(payload: {
  poDocEntry: number
  docNumber?: number
  wtCode?: string
  modeOfPayment?: string
  account?: string
  journalRemark?: string
  referenceNo?: string
  postingDate?: string
  paymentDate?: string
  lines: CreateBatchLinePayload[]
}): Promise<StageWisePaymentBatch> {
  return apiPost<StageWisePaymentBatch>('/stage-wise-payment-batches', payload)
}

export async function getStageWisePaymentBatch(batchId: number): Promise<StageWisePaymentBatch> {
  return apiGet<StageWisePaymentBatch>(`/stage-wise-payment-batches/${batchId}`)
}

export async function getBatchByStageWisePaymentId(stageWisePaymentId: number): Promise<StageWisePaymentBatch> {
  return apiGet<StageWisePaymentBatch>(`/stage-wise-payment-batches/by-stage-wise-payment/${stageWisePaymentId}`)
}

export async function getBatchByApprovalRequestId(approvalRequestId: number): Promise<StageWisePaymentBatch | null> {
  try {
    return await apiGet<StageWisePaymentBatch>(`/stage-wise-payment-batches/by-approval/${approvalRequestId}`)
  } catch {
    return null
  }
}

export interface CancelBatchResult {
  success: boolean
  operations: { success: boolean; message: string }[]
}

export async function cancelStageWisePaymentBatch(batchId: number): Promise<CancelBatchResult> {
  return apiPost<CancelBatchResult>(`/stage-wise-payment-batches/${batchId}/cancel`)
}

export async function deleteStageWisePaymentBatch(batchId: number) {
  return apiDelete(`/stage-wise-payment-batches/${batchId}`)
}

export async function downloadStageWisePaymentBatchPdf(batchId: number): Promise<Blob> {
  return apiDownloadGet(`/stage-wise-payment-batches/${batchId}/pdf`)
}
