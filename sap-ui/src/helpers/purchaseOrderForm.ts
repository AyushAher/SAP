import type { PaymentTermRow, PurchaseOrderLineItem, PurchaseOrderLogistics, PurchaseOrderOtherTerms } from '@/types/purchaseOrder'
import { MAX_PAYMENT_TERMS } from '@/types/purchaseOrder'

type PoRecord = Record<string, unknown>

function readString(source: PoRecord, ...keys: string[]): string {
  for (const key of keys) {
    const value = source[key]
    if (value != null && value !== '') return String(value)
  }
  return ''
}

function readNumber(source: PoRecord, ...keys: string[]): number | undefined {
  for (const key of keys) {
    const value = source[key]
    if (value == null || value === '') continue
    const num = Number(value)
    if (Number.isFinite(num)) return num
  }
  return undefined
}

export function parsePaymentTermsFromPo(po: PoRecord): PaymentTermRow[] {
  const terms: PaymentTermRow[] = []
  for (let i = 1; i <= MAX_PAYMENT_TERMS; i += 1) {
    const type = readString(po, `U_T${i}`, `UType${i}`)
    const basic = readNumber(po, `U_B${i}`, `UBasic${i}`)
    const gst = readNumber(po, `U_G${i}`, `UGst${i}`)
    const stage = readString(po, `U_S${i}`, `UStage${i}`)
    const desc = readString(po, `U_D${i}`, `UDes${i}`)
    if (type || basic != null || gst != null || stage || desc) {
      terms.push({ id: i, type: type || undefined, basic, gst, stage: stage || undefined, desc: desc || undefined })
    }
  }
  return terms
}

export function applyPaymentTermsToPo(po: PoRecord, terms: PaymentTermRow[]): PoRecord {
  const next = { ...po }
  for (let i = 1; i <= MAX_PAYMENT_TERMS; i += 1) {
    delete next[`U_B${i}`]
    delete next[`U_G${i}`]
    delete next[`U_D${i}`]
    delete next[`U_S${i}`]
    delete next[`U_T${i}`]
  }
  for (const term of terms.slice(0, MAX_PAYMENT_TERMS)) {
    const slot = term.id
    if (term.basic != null) next[`U_B${slot}`] = term.basic
    if (term.gst != null) next[`U_G${slot}`] = term.gst
    if (term.desc) next[`U_D${slot}`] = term.desc
    if (term.stage) next[`U_S${slot}`] = term.stage
    if (term.type) next[`U_T${slot}`] = term.type
  }
  return next
}

export function nextPaymentTermSlot(existing: PaymentTermRow[]): number | null {
  const used = new Set(existing.map((t) => t.id))
  for (let i = 1; i <= MAX_PAYMENT_TERMS; i += 1) {
    if (!used.has(i)) return i
  }
  return null
}

export function readLogisticsFromPo(po: PoRecord): PurchaseOrderLogistics {
  return {
    dispatchTo: readString(po, 'U_DispatchTo', 'ShipToCode'),
    contactPerson: readString(po, 'U_ContactPerson', 'ContactPersonCode'),
    priceBasis: readString(po, 'U_PriceBasis'),
    modeOfTransport: readString(po, 'U_ModeOfTransport', 'TransportationCode'),
    materialOutwardDoc: readString(po, 'U_MatOutDoc'),
    goodsIssueTransfer: readString(po, 'U_GoodsIssue'),
    materialInwardDoc: readString(po, 'U_MatInDoc'),
    goodsReceiptTransfer: readString(po, 'U_GoodsReceipt'),
  }
}

export function applyLogisticsToPo(po: PoRecord, logistics: PurchaseOrderLogistics): PoRecord {
  return {
    ...po,
    U_DispatchTo: logistics.dispatchTo || undefined,
    ShipToCode: logistics.dispatchTo || undefined,
    U_ContactPerson: logistics.contactPerson || undefined,
    U_PriceBasis: logistics.priceBasis || undefined,
    U_ModeOfTransport: logistics.modeOfTransport || undefined,
    TransportationCode: logistics.modeOfTransport ? Number(logistics.modeOfTransport) || logistics.modeOfTransport : undefined,
    U_MatOutDoc: logistics.materialOutwardDoc || undefined,
    U_GoodsIssue: logistics.goodsIssueTransfer || undefined,
    U_MatInDoc: logistics.materialInwardDoc || undefined,
    U_GoodsReceipt: logistics.goodsReceiptTransfer || undefined,
  }
}

export function readOtherTermsFromPo(po: PoRecord): PurchaseOrderOtherTerms {
  return {
    deliveryTerms: readString(po, 'U_DelTerms'),
    inspectionBy: readString(po, 'U_InspectionBy'),
    transportation: readString(po, 'U_Transportation'),
    supervision: readString(po, 'U_Supervision'),
    transitInsurance: readString(po, 'U_TransitIns'),
    drawingDocuments: readString(po, 'U_DrawDocs'),
    loading: readString(po, 'U_Loading'),
    warranty: readString(po, 'U_Warranty'),
    unloading: readString(po, 'U_Unloading'),
    otherRemark: readString(po, 'U_OtherRemark'),
    painting: readString(po, 'U_Painting'),
    testCertificates: readString(po, 'U_TestCerts'),
  }
}

export function applyOtherTermsToPo(po: PoRecord, terms: PurchaseOrderOtherTerms): PoRecord {
  return {
    ...po,
    U_DelTerms: terms.deliveryTerms || undefined,
    U_InspectionBy: terms.inspectionBy || undefined,
    U_Transportation: terms.transportation || undefined,
    U_Supervision: terms.supervision || undefined,
    U_TransitIns: terms.transitInsurance || undefined,
    U_DrawDocs: terms.drawingDocuments || undefined,
    U_Loading: terms.loading || undefined,
    U_Warranty: terms.warranty || undefined,
    U_Unloading: terms.unloading || undefined,
    U_OtherRemark: terms.otherRemark || undefined,
    U_Painting: terms.painting || undefined,
    U_TestCerts: terms.testCertificates || undefined,
  }
}

export interface PurchaseOrderTotals {
  totalBeforeDiscount: number
  tax: number
  roundingOff: number
  totalPaymentDue: number
}

export function calculateLineTotals(line: PurchaseOrderLineItem, taxRate = 0): PurchaseOrderLineItem {
  const lineTotal = (line.UnitPrice ?? 0) * (line.Quantity ?? 0)
  const taxTotal = line.TaxTotal ?? (lineTotal * taxRate) / 100
  const taxableAmount = lineTotal
  const weightKg = (line.WeightKg ?? 0) * (line.Quantity ?? 0)
  return {
    ...line,
    LineTotal: lineTotal,
    TaxTotal: taxTotal,
    TaxableAmount: taxableAmount,
    GrossTotal: lineTotal + taxTotal,
    WeightKg: line.WeightKg ?? weightKg,
  }
}

export function calculatePurchaseOrderTotals(
  lines: PurchaseOrderLineItem[],
  roundingOff = 0,
): PurchaseOrderTotals {
  const totalBeforeDiscount = lines.reduce((sum, line) => sum + (line.LineTotal ?? (line.UnitPrice ?? 0) * (line.Quantity ?? 0)), 0)
  const tax = lines.reduce((sum, line) => sum + (line.TaxTotal ?? 0), 0)
  const totalPaymentDue = totalBeforeDiscount + tax + roundingOff
  return { totalBeforeDiscount, tax, roundingOff, totalPaymentDue }
}

export function formatPoAmount(value: number | undefined | null): string {
  return Number(value ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function paymentTermDisplayLabel(term: PaymentTermRow): string {
  if (term.desc) return term.desc
  const parts = [
    term.type,
    term.basic != null ? `Basic ${term.basic}%` : '',
    term.gst != null ? `GST ${term.gst}%` : '',
    term.stage,
  ].filter(Boolean)
  return parts.join(' · ') || `Term ${term.id}`
}
