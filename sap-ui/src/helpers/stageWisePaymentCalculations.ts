export interface PaymentTermUdf {
  id?: number
  desc?: string
  type?: string
  basic?: number
  gst?: number
  stage?: string
}

export interface StageWisePayment {
  id: number
  paymentTermsType?: number
  stageDesc?: string
  bank?: string
  grossAmount?: number
  gstAmount?: number
  tds?: number
  status: number | string
  docNumber?: number
  approvalRequestId?: string
  apDownPaymentInvoiceEntryNumber?: string
  apInvoiceDocEntry?: string
  wtCode?: string
  utrNo?: string
  utrDate?: string
  createdOn?: string
}

export interface ApInvoice {
  DocEntry?: number
  DocNum?: number
  NumAtCard?: string
  DocTotal?: number
  PaidToDate?: number
  WTAmount?: number
  DocumentStatus?: string
}

export interface PurchaseOrderSummary {
  DocEntry?: number
  DocNum?: number
  CardCode?: string
  CardName?: string
  Project?: string
  DocTotal?: number
  VatSum?: number
  DocumentStatus?: string
  BPLId?: number
}

export interface PaymentSummaryRow {
  label: string
  poValue: number
  requested: number
  paid: number
  balance: number
}

export function formatAmount(value: number | undefined | null): string {
  return Number(value ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function paymentTermLabel(term: PaymentTermUdf): string {
  if (term.desc) return term.desc
  return `Basic ${term.basic ?? 0}% & GST ${term.gst ?? 0}%`
}

export function isPaymentTermSelectable(term: PaymentTermUdf): boolean {
  return Boolean(term.desc) || term.basic != null || term.gst != null
}

export function normalizeStatus(status: number | string): string {
  if (typeof status === 'string') {
    if (['Created', 'Approval Pending', 'Approved', 'Cancelled'].includes(status)) return status
    if (status === '0') return 'Created'
    if (status === '1') return 'Approval Pending'
    if (status === '2') return 'Approved'
    if (status === '3') return 'Cancelled'
    return status
  }
  switch (status) {
    case 0: return 'Created'
    case 1: return 'Approval Pending'
    case 2: return 'Approved'
    case 3: return 'Cancelled'
    default: return String(status)
  }
}

export function isPaidRecord(record: StageWisePayment): boolean {
  const status = typeof record.status === 'number' ? record.status : Number(record.status)
  return Boolean(record.apDownPaymentInvoiceEntryNumber) && (status === 0 || status === 2 || record.status === 'Added' || record.status === 'Approved')
}

export function getPayableAmount(
  po: PurchaseOrderSummary,
  paymentTerms: PaymentTermUdf[],
  activeRecords: StageWisePayment[],
  selectedTerm: PaymentTermUdf | undefined,
  paymentTermId?: number,
  totalBasic = 0,
): number {
  const termId = paymentTermId ?? selectedTerm?.id
  const paymentTerm = paymentTerms.find((x) => x.id === termId)
  if (!paymentTerm) return 0

  const alreadyPaid = activeRecords
    .filter((x) => x.paymentTermsType === paymentTerm.id)
    .reduce((sum, x) => sum + (x.grossAmount ?? 0) + (x.gstAmount ?? 0), 0)

  return round2(
    (totalBasic * (paymentTerm.basic ?? 0)) / 100
    + ((po.VatSum ?? 0) * (paymentTerm.gst ?? 0)) / 100
    - alreadyPaid,
  )
}

export function getAlreadyPaidAmountForPaymentTerms(
  po: PurchaseOrderSummary,
  paymentTerms: PaymentTermUdf[],
  activeRecords: StageWisePayment[],
  selectedTerm: PaymentTermUdf | undefined,
  paymentTermsType: number | undefined,
  totalBasic: number,
): number {
  const selectedPaymentTermPayable = getPayableAmount(po, paymentTerms, activeRecords, selectedTerm, paymentTermsType, totalBasic)
  let negativePayableAmount = 0
  const ids: number[] = []

  for (const paymentTerm of paymentTerms) {
    if (selectedTerm?.id === paymentTerm.id) continue
    const payableAmt = getPayableAmount(po, paymentTerms, activeRecords, paymentTerm, paymentTerm.id, totalBasic)
    if (payableAmt < 0 && paymentTerm.id != null) {
      negativePayableAmount += payableAmt
      ids.push(paymentTerm.id)
    }
  }

  if (ids.includes(paymentTermsType ?? 0)) {
    return getPayableAmount(po, paymentTerms, activeRecords, selectedTerm, paymentTermsType, totalBasic)
  }
  if (ids.length > 0 && Math.max(...ids) + 1 === paymentTermsType) {
    return selectedPaymentTermPayable + negativePayableAmount
  }
  return selectedPaymentTermPayable
}

export function getApInvoiceBalanceDue(
  po: PurchaseOrderSummary | null | undefined,
  selectedTerm: PaymentTermUdf | undefined,
  selectedApInvoice: ApInvoice | undefined,
  activeRecords: StageWisePayment[],
  apInvoiceDocEntry: string,
): number {
  if (po?.DocumentStatus !== 'bost_Close' && selectedTerm?.type !== 'Invoice' && selectedTerm?.type !== 'Retention') {
    return 0
  }

  const sapBalance = (selectedApInvoice?.DocTotal ?? 0)
    - (selectedApInvoice?.PaidToDate ?? 0)
    + (selectedApInvoice?.WTAmount ?? 0)

  const usedAmount = activeRecords
    .filter((x) => x.apInvoiceDocEntry === apInvoiceDocEntry && !x.apDownPaymentInvoiceEntryNumber)
    .reduce((sum, x) => sum + (x.grossAmount ?? 0) + (x.gstAmount ?? 0), 0)

  return round2(sapBalance - usedAmount)
}

export function resolveDisplayPayable(
  po: PurchaseOrderSummary,
  paymentTerms: PaymentTermUdf[],
  activeRecords: StageWisePayment[],
  selectedTerm: PaymentTermUdf | undefined,
  apInvoiceDocEntry: string,
  selectedApInvoice: ApInvoice | undefined,
  totalBasic: number,
): number {
  if (selectedTerm?.type === 'Invoice' || selectedTerm?.type === 'Retention') {
    return getApInvoiceBalanceDue(po, selectedTerm, selectedApInvoice, activeRecords, apInvoiceDocEntry)
  }
  return getAlreadyPaidAmountForPaymentTerms(
    po,
    paymentTerms,
    activeRecords,
    selectedTerm,
    selectedTerm?.id,
    totalBasic,
  )
}

function round2(value: number): number {
  return Math.round(value * 100) / 100
}
