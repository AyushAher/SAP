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

function firstNonEmpty(...values: Array<string | undefined | null>): string {
  for (const value of values) {
    if (value == null) continue
    const trimmed = String(value).trim()
    if (trimmed) return trimmed
  }
  return ''
}

/**
 * Resolve line UoMs for display/save.
 * - Purchase UoM: defaults from item master PurchaseUnit (falls back to InventoryUOM); stays user-editable.
 * - Stock UoM: always prefers item master InventoryUOM (read-only / disabled in the UI).
 */
export function resolveLineUoms(
  line: PurchaseOrderLineItem,
  master?: ItemMasterUoms,
): { purchaseUom?: string; stockUom?: string } {
  const purchaseUom = firstNonEmpty(line.UoMCode, line.UomName, master?.purchaseUom)
  const stockUom = firstNonEmpty(master?.stockUom, line.StockUom)
  return {
    purchaseUom: purchaseUom || undefined,
    stockUom: stockUom || undefined,
  }
}

/** Defaults both UoMs from SAP item master fields (PurchaseUnit / InventoryUOM). */
export function uomsFromItemMaster(meta?: {
  PurchaseUnit?: string
  InventoryUom?: string
} | null): { purchaseUom: string; stockUom: string } {
  return {
    purchaseUom: firstNonEmpty(meta?.PurchaseUnit, meta?.InventoryUom),
    stockUom: firstNonEmpty(meta?.InventoryUom, meta?.PurchaseUnit),
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
