import { apiGet, apiPost, apiDelete, apiDownloadGet } from '@/helpers/api/client'
import type {
  ApInvoice,
  PaymentTermUdf,
  PurchaseOrderSummary,
  StageWisePayment,
} from '@/helpers/stageWisePaymentCalculations'

export interface PaymentSummaryRow {
  label: string
  poValue: number
  requested: number
  paid: number
  balance: number
}

export interface BankOption {
  key: string
  value: string
}

export interface WtCodeOption {
  wtCode: string
  wtName?: string
  rate?: number
}

export interface StageWisePaymentPageData {
  purchaseOrder?: PurchaseOrderSummary
  projectName?: string
  totalBasic: number
  balancePayment: number
  paymentTerms: PaymentTermUdf[]
  tableRecords: StageWisePayment[]
  activeRecords: StageWisePayment[]
  banks: BankOption[]
  bankLabels: Record<string, string>
  apInvoices: ApInvoice[]
  withholdingTaxCodes: WtCodeOption[]
  paymentSummary: PaymentSummaryRow[]
}

export type { PaymentTermUdf, StageWisePayment, ApInvoice, PurchaseOrderSummary }

export async function getStageWisePaymentPageData(poDocEntry: number): Promise<StageWisePaymentPageData> {
  return apiGet<StageWisePaymentPageData>(`/stage-wise-payments/page-data/${poDocEntry}`)
}

export async function createStageWisePayment(payload: Record<string, unknown>) {
  return apiPost('/stage-wise-payments', payload)
}

export async function deleteStageWisePayment(id: number) {
  return apiDelete(`/stage-wise-payments/${id}`)
}

export async function cancelStageWisePayment(id: number) {
  return apiPost(`/stage-wise-payments/${id}/cancel`)
}

export async function downloadStageWisePaymentPdf(id: number, poDocEntry: number): Promise<Blob> {
  return apiDownloadGet(`/stage-wise-payments/${id}/pdf?poDocEntry=${poDocEntry}`)
}
