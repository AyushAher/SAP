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
  outgoingPaymentNumber?: string
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
  DocDate?: string
  PostingDate?: string
}

export function isPoClosed(po?: PurchaseOrderSummary | null): boolean {
  const status = (po?.DocumentStatus ?? '').trim().toLowerCase()
  return status === 'bost_close' || status === 'close' || status === 'closed'
}

export function shouldShowApInvoiceSelector(
  po: PurchaseOrderSummary | null | undefined,
  selectedTerm: PaymentTermUdf | undefined,
): boolean {
  return isPoClosed(po) || selectedTerm?.type === 'Invoice' || selectedTerm?.type === 'Retention'
}

export function isBatchPaymentAvailable(
  _po?: PurchaseOrderSummary | null,
  paymentTerms?: PaymentTermUdf[],
  apInvoices?: ApInvoice[],
): boolean {
  const hasSelectableTerms = (paymentTerms ?? []).some(isPaymentTermSelectable)
  if (hasSelectableTerms)
    return true
  return (apInvoices?.length ?? 0) > 0
}

export function batchRowRequiresApInvoice(
  po: PurchaseOrderSummary | null | undefined,
  paymentTerms: PaymentTermUdf[],
  selectedTermIds: number[],
): boolean {
  if (isPoClosed(po))
    return true
  return selectedTermIds.some((id) => {
    const term = paymentTerms.find((t) => t.id === id)
    return isVendorApInvoicePaymentTerm(term)
  })
}

export function isVendorApInvoicePaymentTerm(term: PaymentTermUdf | undefined): boolean {
  return term?.type === 'Invoice' || term?.type === 'Retention'
}

/** AP invoice / vendor outgoing payments must use batch payment only. */
export function requiresBatchPayment(
  po: PurchaseOrderSummary | null | undefined,
  selectedTerm: PaymentTermUdf | undefined,
  apInvoiceDocEntry?: string,
): boolean {
  return isPoClosed(po)
    || isVendorApInvoicePaymentTerm(selectedTerm)
    || Boolean(apInvoiceDocEntry?.trim())
}

export function isSinglePaymentTermAllowed(
  po: PurchaseOrderSummary | null | undefined,
  term: PaymentTermUdf,
): boolean {
  return !requiresBatchPayment(po, term)
}

export function filterSinglePaymentTerms(
  po: PurchaseOrderSummary | null | undefined,
  terms: PaymentTermUdf[],
): PaymentTermUdf[] {
  return terms.filter((t) => isSinglePaymentTermAllowed(po, t))
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

export function shouldUseApInvoicePayable(
  po: PurchaseOrderSummary | null | undefined,
  selectedTerm: PaymentTermUdf | undefined,
  apInvoiceDocEntry?: string,
): boolean {
  if (!shouldShowApInvoiceSelector(po, selectedTerm))
    return false
  if (isPoClosed(po) && isVendorApInvoicePaymentTerm(selectedTerm))
    return true
  return Boolean(apInvoiceDocEntry?.trim())
}

export function shouldUseApInvoiceBalanceForBatchRow(
  po: PurchaseOrderSummary,
  paymentTerms: PaymentTermUdf[],
  selectedTermIds: number[],
  apInvoiceDocEntry?: string,
): boolean {
  const hasInvoiceRetentionTerm = selectedTermIds.some((id) => {
    const term = paymentTerms.find((t) => t.id === id)
    return isVendorApInvoicePaymentTerm(term)
  })

  if (isPoClosed(po) && hasInvoiceRetentionTerm)
    return true

  if (!apInvoiceDocEntry?.trim())
    return false

  return isPoClosed(po) || hasInvoiceRetentionTerm
}

export function getApInvoiceBalanceDue(
  po: PurchaseOrderSummary | null | undefined,
  selectedTerm: PaymentTermUdf | undefined,
  selectedApInvoice: ApInvoice | undefined,
  activeRecords: StageWisePayment[],
  apInvoiceDocEntry: string,
): number {
  if (!shouldUseApInvoicePayable(po, selectedTerm, apInvoiceDocEntry)) {
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
  _apInvoiceDocEntry: string,
  _selectedApInvoice: ApInvoice | undefined,
  totalBasic: number,
): number {
  return getAlreadyPaidAmountForPaymentTerms(
    po,
    paymentTerms,
    activeRecords,
    selectedTerm,
    selectedTerm?.id,
    totalBasic,
  )
}

function resolveRepresentativePaymentTerm(
  paymentTerms: PaymentTermUdf[],
  selectedTermIds: number[],
): PaymentTermUdf | undefined {
  return selectedTermIds
    .map((id) => paymentTerms.find((t) => t.id === id))
    .find((t) => t?.type === 'Invoice' || t?.type === 'Retention')
    ?? selectedTermIds.map((id) => paymentTerms.find((t) => t.id === id)).find(Boolean)
}

export function resolveBatchRowPayable(
  po: PurchaseOrderSummary,
  paymentTerms: PaymentTermUdf[],
  activeRecords: StageWisePayment[],
  selectedTermIds: number[],
  apInvoice: ApInvoice | undefined,
  apInvoiceDocEntry: string,
  totalBasic: number,
): { balanceDue: number; payable: number } {
  if (selectedTermIds.length === 0) {
    return { balanceDue: 0, payable: 0 }
  }

  const representativeTerm = resolveRepresentativePaymentTerm(paymentTerms, selectedTermIds)
  const showApBalance = shouldUseApInvoiceBalanceForBatchRow(
    po,
    paymentTerms,
    selectedTermIds,
    apInvoiceDocEntry,
  )
  const balanceDue = showApBalance
    ? getApInvoiceBalanceDue(
      po,
      representativeTerm,
      apInvoice,
      activeRecords,
      apInvoiceDocEntry,
    )
    : 0

  const payable = round2(
    [...new Set(selectedTermIds)].reduce((sum, termId) => {
      const term = paymentTerms.find((t) => t.id === termId)
      if (!term) return sum
      return sum + getAlreadyPaidAmountForPaymentTerms(
        po,
        paymentTerms,
        activeRecords,
        term,
        termId,
        totalBasic,
      )
    }, 0),
  )

  return { balanceDue, payable }
}

function round2(value: number): number {
  return Math.round(value * 100) / 100
}

export interface BatchRowAmountFields {
  apInvoiceDocEntry?: string
  amount?: string | number
  baseBalanceDue?: number
  basePayable?: number
  balanceDue?: number
  payable?: number
  paymentTermsTypes?: Array<string | number>
}

export interface BatchAdjustmentContext {
  po: PurchaseOrderSummary
  paymentTerms: PaymentTermUdf[]
  activeRecords: StageWisePayment[]
  totalBasic: number
  apInvoices?: ApInvoice[]
}

export function formatBatchRowContextLabel(
  row: BatchRowAmountFields,
  context: BatchAdjustmentContext,
): string {
  const parts: string[] = []

  const stageLabels = [...new Set((row.paymentTermsTypes ?? []).map(Number))]
    .map((id) => context.paymentTerms.find((t) => t.id === id))
    .filter((term): term is PaymentTermUdf => term != null)
    .map((term) => paymentTermLabel(term))

  if (stageLabels.length > 0)
    parts.push(`payment stage: ${stageLabels.join(', ')}`)

  if (row.apInvoiceDocEntry?.trim()) {
    const invoice = context.apInvoices?.find(
      (inv) => String(inv.DocEntry ?? '') === row.apInvoiceDocEntry,
    )
    const apLabel = invoice
      ? `${invoice.DocNum ?? ''}:${invoice.NumAtCard ?? ''}`.replace(/^:|:$/g, '') || row.apInvoiceDocEntry
      : row.apInvoiceDocEntry
    parts.push(`AP invoice: ${apLabel}`)
  }

  return parts.length > 0 ? parts.join('; ') : 'row'
}

export function getPaymentTermPayable(
  context: BatchAdjustmentContext,
  termId: number,
): number {
  const term = context.paymentTerms.find((t) => t.id === termId)
  if (!term)
    return 0
  return getAlreadyPaidAmountForPaymentTerms(
    context.po,
    context.paymentTerms,
    context.activeRecords,
    term,
    termId,
    context.totalBasic,
  )
}

/** Split a row amount across payment terms proportionally to each term's payable. */
export function allocateRowAmountByPaymentTerm(
  amount: number,
  termIds: number[],
  context: BatchAdjustmentContext,
): Record<number, number> {
  const uniqueTermIds = [...new Set(termIds)]
  if (uniqueTermIds.length === 0 || amount <= 0)
    return {}

  const weights = uniqueTermIds.map((termId) => ({
    termId,
    weight: Math.max(0, getPaymentTermPayable(context, termId)),
  }))
  const totalWeight = weights.reduce((sum, item) => sum + item.weight, 0)
  if (totalWeight <= 0) {
    const evenShare = round2(amount / uniqueTermIds.length)
    return Object.fromEntries(uniqueTermIds.map((termId) => [termId, evenShare]))
  }

  const allocations: Record<number, number> = {}
  let allocated = 0
  weights.forEach((item, index) => {
    if (index === weights.length - 1) {
      allocations[item.termId] = round2(amount - allocated)
      return
    }
    const share = round2((amount * item.weight) / totalWeight)
    allocations[item.termId] = share
    allocated += share
  })
  return allocations
}

export function getPriorBatchAllocatedAmountForPaymentTerm<T extends BatchRowAmountFields>(
  rows: T[],
  rowIndex: number,
  termId: number,
  context: BatchAdjustmentContext,
): number {
  return rows.slice(0, rowIndex).reduce((sum, row) => {
    const amount = Number(row.amount) || 0
    if (amount <= 0)
      return sum
    const termIds = [...new Set((row.paymentTermsTypes ?? []).map(Number))]
    if (!termIds.includes(termId))
      return sum
    const allocations = allocateRowAmountByPaymentTerm(amount, termIds, context)
    return sum + (allocations[termId] ?? 0)
  }, 0)
}

export function computeSequentialStageRowPayable(
  row: BatchRowAmountFields,
  rowIndex: number,
  rows: BatchRowAmountFields[],
  context: BatchAdjustmentContext,
): number {
  const termIds = [...new Set((row.paymentTermsTypes ?? []).map(Number))]
  if (termIds.length === 0)
    return 0

  return round2(termIds.reduce((sum, termId) => {
    const termPayable = getPaymentTermPayable(context, termId)
    const priorAllocated = getPriorBatchAllocatedAmountForPaymentTerm(rows, rowIndex, termId, context)
    return sum + Math.max(0, termPayable - priorAllocated)
  }, 0))
}

export function getTotalAllocatedAmountByPaymentTerm<T extends BatchRowAmountFields>(
  rows: T[],
  context: BatchAdjustmentContext,
): Record<number, number> {
  const totals: Record<number, number> = {}
  for (const row of rows) {
    const amount = Number(row.amount) || 0
    if (amount <= 0)
      continue
    const termIds = [...new Set((row.paymentTermsTypes ?? []).map(Number))]
    const allocations = allocateRowAmountByPaymentTerm(amount, termIds, context)
    for (const [termId, allocated] of Object.entries(allocations)) {
      const id = Number(termId)
      totals[id] = round2((totals[id] ?? 0) + allocated)
    }
  }
  return totals
}

export function validateBatchPaymentAmounts<T extends BatchRowAmountFields>(
  rows: T[],
  context: BatchAdjustmentContext,
): string | null {
  const adjustedRows = applySequentialBatchRowAdjustments(rows, context)

  for (const [index, row] of adjustedRows.entries()) {
    const amount = Number(row.amount) || 0
    if (amount <= 0)
      return 'Each row amount must be greater than zero.'
    if (amount > (row.payable ?? 0) + 0.001) {
      const rowContext = formatBatchRowContextLabel(row, context)
      return `Net amount cannot exceed payable (${formatAmount(row.payable)}) for row ${index + 1} (${rowContext}).`
    }

    const termIds = (row.paymentTermsTypes ?? []).map(Number)
    const requiresAp = batchRowRequiresApInvoice(context.po, context.paymentTerms, termIds)
    if (requiresAp && amount > (row.balanceDue ?? 0) + 0.001) {
      const rowContext = formatBatchRowContextLabel(row, context)
      return `Net amount cannot exceed AP invoice balance due (${formatAmount(row.balanceDue)}) for row ${index + 1} (${rowContext}).`
    }
  }

  const allocatedByTerm = getTotalAllocatedAmountByPaymentTerm(rows, context)
  for (const [termId, allocated] of Object.entries(allocatedByTerm)) {
    const payable = getPaymentTermPayable(context, Number(termId))
    if (allocated > payable + 0.001) {
      const term = context.paymentTerms.find((t) => t.id === Number(termId))
      const label = term ? paymentTermLabel(term) : `Payment type ${termId}`
      return `Total net amount for ${label} (${formatAmount(allocated)}) exceeds payable (${formatAmount(payable)}).`
    }
  }

  return null
}

export function validateBatchComposition<T extends BatchRowAmountFields>(
  rows: T[],
  context: BatchAdjustmentContext,
): string | null {
  let hasApRows = false
  let hasDownPaymentRows = false

  for (const row of rows) {
    const termIds = (row.paymentTermsTypes ?? []).map(Number)
    if (termIds.length === 0) continue
    if (batchRowRequiresApInvoice(context.po, context.paymentTerms, termIds))
      hasApRows = true
    else
      hasDownPaymentRows = true
  }

  if (hasApRows && hasDownPaymentRows) {
    return 'A batch cannot mix AP invoice payments with down payment stages. Create separate batches for each payment type.'
  }

  return null
}

/** Sum of net amounts from earlier rows sharing the same AP invoice. */
export function getPriorBatchRowAppliedAmount<T extends BatchRowAmountFields>(
  rows: T[],
  rowIndex: number,
  apInvoiceDocEntry: string,
): number {
  return rows
    .slice(0, rowIndex)
    .filter((row) => row.apInvoiceDocEntry === apInvoiceDocEntry)
    .reduce((sum, row) => sum + (Number(row.amount) || 0), 0)
}

/** Apply sequential balance/payable deductions for duplicate AP invoices and payment stages in a batch. */
export function applySequentialBatchRowAdjustments<T extends BatchRowAmountFields>(
  rows: T[],
  context?: BatchAdjustmentContext,
): T[] {
  return rows.map((row, index) => {
    const baseBalanceDue = row.baseBalanceDue ?? row.balanceDue ?? 0
    const basePayable = row.basePayable ?? row.payable ?? 0

    if (row.apInvoiceDocEntry) {
      const priorApplied = getPriorBatchRowAppliedAmount(rows, index, row.apInvoiceDocEntry)
      const balanceDue = round2(Math.max(0, baseBalanceDue - priorApplied))
      const payable = context && (row.paymentTermsTypes?.length ?? 0) > 0
        ? computeSequentialStageRowPayable(row, index, rows, context)
        : round2(Math.max(0, basePayable - priorApplied))
      return {
        ...row,
        baseBalanceDue,
        basePayable,
        balanceDue,
        payable,
      }
    }

    if (context && (row.paymentTermsTypes?.length ?? 0) > 0) {
      const payable = computeSequentialStageRowPayable(row, index, rows, context)
      return {
        ...row,
        baseBalanceDue,
        basePayable,
        balanceDue: baseBalanceDue,
        payable,
      }
    }

    return {
      ...row,
      baseBalanceDue,
      basePayable,
      balanceDue: baseBalanceDue,
      payable: basePayable,
    }
  })
}
