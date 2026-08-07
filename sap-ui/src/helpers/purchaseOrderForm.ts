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
    dispatchAddress: readString(po, 'U_DispachAdd'),
    contactPerson: readString(po, 'U_ContactPerson', 'ContactPersonCode'),
    priceBasis: readString(po, 'U_PriceBasis'),
    modeOfTransport: readString(po, 'U_ModeOfTransport'),
  }
}

export function applyLogisticsToPo(po: PoRecord, logistics: PurchaseOrderLogistics): PoRecord {
  return {
    ...po,
    U_DispatchTo: logistics.dispatchTo || undefined,
    ShipToCode: logistics.dispatchTo || undefined,
    U_DispachAdd: logistics.dispatchAddress || undefined,
    U_ContactPerson: logistics.contactPerson || undefined,
    U_PriceBasis: logistics.priceBasis || undefined,
    U_ModeOfTransport: logistics.modeOfTransport || undefined,
  }
}

/** OPOR Other Terms UDFs (Service Layer names from UserFieldsMD). */
export function readOtherTermsFromPo(po: PoRecord): PurchaseOrderOtherTerms {
  return {
    deliveryTerms: readString(po, 'U_DL', 'U_DelTerms'),
    inspectionBy: readString(po, 'U_INSPBY', 'U_InspectionBy'),
    transportation: readString(po, 'U_TRANS', 'U_Transportation'),
    supervision: readString(po, 'U_SUPR', 'U_Supervision'),
    transitInsurance: readString(po, 'U_TRANINSU', 'U_TransitIns'),
    drawingDocuments: readString(po, 'U_DRA_DOC', 'U_DrawDocs'),
    loading: readString(po, 'U_LOAD', 'U_Loading'),
    warranty: readString(po, 'U_WARR', 'U_Warranty'),
    unloading: readString(po, 'U_UN_LOAD', 'U_Unloading'),
    otherRemark: readString(po, 'U_ANOTHREM', 'U_OtherRemark'),
    painting: readString(po, 'U_PAIN', 'U_Painting'),
    testCertificates: readString(po, 'U_TC', 'U_TestCerts'),
  }
}

export function applyOtherTermsToPo(po: PoRecord, terms: PurchaseOrderOtherTerms): PoRecord {
  const next: PoRecord = { ...po }
  // Drop legacy invented names so they are never posted to Service Layer.
  for (const legacy of [
    'U_DelTerms', 'U_InspectionBy', 'U_Transportation', 'U_Supervision', 'U_TransitIns',
    'U_DrawDocs', 'U_Loading', 'U_Warranty', 'U_Unloading', 'U_OtherRemark', 'U_Painting', 'U_TestCerts',
  ]) {
    delete next[legacy]
  }
  next.U_DL = terms.deliveryTerms || undefined
  next.U_INSPBY = terms.inspectionBy || undefined
  next.U_TRANS = terms.transportation || undefined
  next.U_SUPR = terms.supervision || undefined
  next.U_TRANINSU = terms.transitInsurance || undefined
  next.U_DRA_DOC = terms.drawingDocuments || undefined
  next.U_LOAD = terms.loading || undefined
  next.U_WARR = terms.warranty || undefined
  next.U_UN_LOAD = terms.unloading || undefined
  next.U_ANOTHREM = terms.otherRemark || undefined
  next.U_PAIN = terms.painting || undefined
  next.U_TC = terms.testCertificates || undefined
  return next
}

export interface PurchaseOrderTotals {
  totalBeforeDiscount: number
  tax: number
  roundingOff: number
  totalPaymentDue: number
}

export function calculateLineTotals(line: PurchaseOrderLineItem, taxRate = 0): PurchaseOrderLineItem {
  const qty = line.Quantity ?? 0
  const price = line.UnitPrice ?? 0
  const discountPct = line.DiscountPercent ?? 0
  const lineTotal = price * qty * (1 - discountPct / 100)
  const taxTotal = line.TaxTotal ?? (lineTotal * taxRate) / 100
  const withUom = applyStockPurchaseQty(line)
  return {
    ...withUom,
    LineTotal: lineTotal,
    TaxTotal: taxTotal,
    TaxableAmount: lineTotal,
    GrossTotal: lineTotal + taxTotal,
  }
}

export interface ItemMasterUoms {
  purchaseUom?: string
  stockUom?: string
}

/**
 * Purchase UoM defaults from the item master but stays user-editable.
 * Stock UoM is always taken from the item master because it is read-only in the UI.
 */
export function resolveLineUoms(
  line: PurchaseOrderLineItem,
  master?: ItemMasterUoms,
): { purchaseUom?: string; stockUom?: string } {
  const purchaseUom = line.UoMCode ?? line.UomName ?? master?.purchaseUom ?? ''
  const stockUom = master?.stockUom ?? line.StockUom ?? ''
  return {
    purchaseUom: purchaseUom || undefined,
    stockUom: stockUom || undefined,
  }
}

/** Items per unit = Stock Qty ÷ Purchase Qty (SAP UnitsOfMeasurment). */
export function calcItemsPerUnit(stockQty?: number | null, purchaseQty?: number | null): number | undefined {
  if (purchaseQty == null || purchaseQty <= 0 || stockQty == null || !Number.isFinite(stockQty)) return undefined
  return stockQty / purchaseQty
}

/** SAP UseBaseUnits: Inventory UoM Yes when items/unit is 1. */
export function calcUseBaseUnits(itemsPerUnit?: number | null): 'tYES' | 'tNO' | undefined {
  if (itemsPerUnit == null || !Number.isFinite(itemsPerUnit)) return undefined
  return Math.abs(itemsPerUnit - 1) < 1e-9 ? 'tYES' : 'tNO'
}

/** Keep Quantity (purchase), StockQty, UnitsOfMeasurment, and UseBaseUnits in sync. */
export function applyStockPurchaseQty(line: PurchaseOrderLineItem): PurchaseOrderLineItem {
  const purchaseQty = line.Quantity ?? 0
  const stockQty = line.StockQty
  const itemsPerUnit = calcItemsPerUnit(stockQty, purchaseQty)
  return {
    ...line,
    UnitsOfMeasurment: itemsPerUnit,
    UseBaseUnits: calcUseBaseUnits(itemsPerUnit),
  }
}

export function withPurchaseQty(line: PurchaseOrderLineItem, purchaseQty: number): PurchaseOrderLineItem {
  return applyStockPurchaseQty({
    ...line,
    Quantity: purchaseQty,
  })
}

export function withStockQty(line: PurchaseOrderLineItem, stockQty: number): PurchaseOrderLineItem {
  return applyStockPurchaseQty({
    ...line,
    StockQty: stockQty,
  })
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
