import type { PaymentTermRow, PurchaseOrderLineItem, PurchaseOrderLogistics, PurchaseOrderOtherTerms } from '@/types/purchaseOrder'
import {
  GST_PAYMENT_TERM_SLOT,
  GST_PAYMENT_TERM_TYPES,
  MAX_BASIC_PAYMENT_TERMS,
  MAX_PAYMENT_TERMS,
} from '@/types/purchaseOrder'

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

/** GST types store Payment% in U_G11 only; all other types use U_Bn. */
export function isGstPaymentTermType(type?: string | null): boolean {
  const normalized = normalizePaymentTermType(type)
  if (!normalized) return false
  return (GST_PAYMENT_TERM_TYPES as readonly string[]).some(
    (t) => t.localeCompare(normalized, undefined, { sensitivity: 'accent' }) === 0,
  )
}

/** Legacy UI used "Running"; SAP ValidValue is "Proforma". */
export function normalizePaymentTermType(type?: string | null): string {
  const value = (type ?? '').trim()
  if (!value) return ''
  if (value.localeCompare('Running', undefined, { sensitivity: 'accent' }) === 0) return 'Proforma'
  return value
}

/**
 * Resolve the single Payment% for display/edit from stored basic/gst + type.
 * GST-mapped + gst>0 → gst; else basic if basic>0; else gst (legacy mixed rows).
 */
export function resolvePaymentTermPercent(term: Pick<PaymentTermRow, 'type' | 'basic' | 'gst'>): number | undefined {
  const basic = term.basic != null && Number.isFinite(term.basic) && term.basic > 0 ? term.basic : undefined
  const gst = term.gst != null && Number.isFinite(term.gst) && term.gst > 0 ? term.gst : undefined
  if (isGstPaymentTermType(term.type) && gst != null) return gst
  if (basic != null) return basic
  if (gst != null) return gst
  return undefined
}

/** Split a Payment% into basic/gst fields based on type (clears the unused field). */
export function applyPaymentPercentToTerm<T extends Pick<PaymentTermRow, 'type' | 'basic' | 'gst'>>(
  term: T,
  percent: number | undefined,
  type: string | undefined = term.type,
): T {
  const normalizedType = normalizePaymentTermType(type) || undefined
  if (isGstPaymentTermType(normalizedType)) {
    return { ...term, type: normalizedType, basic: undefined, gst: percent }
  }
  return { ...term, type: normalizedType, basic: percent, gst: undefined }
}

export function hasGstPaymentTerm(terms: PaymentTermRow[]): boolean {
  return terms.some((t) => isGstPaymentTermType(t.type) || t.id === GST_PAYMENT_TERM_SLOT)
}

/**
 * Parse OPOR payment-term UDFs.
 * GST% lives only on U_G11 — coalesce legacy GST types from slots 1–10 into slot 11.
 */
export function parsePaymentTermsFromPo(po: PoRecord): PaymentTermRow[] {
  const terms: PaymentTermRow[] = []
  let gstTerm: PaymentTermRow | undefined

  for (let i = 1; i <= MAX_PAYMENT_TERMS; i += 1) {
    const rawType = readString(po, `U_T${i}`, `UType${i}`)
    const type = normalizePaymentTermType(rawType) || undefined
    const basic = readNumber(po, `U_B${i}`, `UBasic${i}`)
    const gst = readNumber(po, `U_G${i}`, `UGst${i}`)
    const stage = readString(po, `U_S${i}`, `UStage${i}`)
    const desc = readString(po, `U_D${i}`, `UDes${i}`)
    const isGstSlot = i === GST_PAYMENT_TERM_SLOT
    const isGstType = isGstPaymentTermType(type)
    const gstValue = gst != null && gst > 0 ? gst : undefined

    if (isGstSlot || isGstType || (isGstSlot && gstValue != null)) {
      // Prefer explicit slot 11; otherwise first legacy GST row wins.
      if (!gstTerm || isGstSlot) {
        gstTerm = {
          id: GST_PAYMENT_TERM_SLOT,
          type: type || gstTerm?.type,
          basic: undefined,
          gst: gstValue ?? gstTerm?.gst,
          stage: stage || gstTerm?.stage,
          desc: desc || gstTerm?.desc,
        }
      }
      continue
    }

    if (type || basic != null || stage || desc) {
      terms.push({
        id: i,
        type,
        basic: basic != null && basic > 0 ? basic : basic === 0 ? 0 : undefined,
        gst: undefined,
        stage: stage || undefined,
        desc: desc || undefined,
      })
    }
  }

  // Slot 11 may only have U_G11 with empty type.
  if (gstTerm && (gstTerm.type || gstTerm.gst != null || gstTerm.stage || gstTerm.desc)) {
    terms.push(gstTerm)
  }

  return terms.sort((a, b) => a.id - b.id)
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

  // Always clear GST% on slots 1–10 — only U_G11 may hold GST.
  for (let i = 1; i <= MAX_BASIC_PAYMENT_TERMS; i += 1) {
    next[`U_G${i}`] = 0
  }

  const basicTerms = terms.filter((t) => !isGstPaymentTermType(t.type) && t.id !== GST_PAYMENT_TERM_SLOT)
  const gstTerms = terms.filter((t) => isGstPaymentTermType(t.type) || t.id === GST_PAYMENT_TERM_SLOT)
  const gstTerm = gstTerms[0]

  for (const term of basicTerms.slice(0, MAX_BASIC_PAYMENT_TERMS)) {
    const slot = Math.min(Math.max(term.id, 1), MAX_BASIC_PAYMENT_TERMS)
    const mapped = applyPaymentPercentToTerm(
      term,
      resolvePaymentTermPercent(term) ?? term.basic,
      term.type,
    )
    if (mapped.type || mapped.basic != null || mapped.stage || mapped.desc) {
      next[`U_B${slot}`] = mapped.basic ?? 0
      next[`U_G${slot}`] = 0
      if (mapped.desc) next[`U_D${slot}`] = mapped.desc
      if (mapped.stage) next[`U_S${slot}`] = mapped.stage
      if (mapped.type) next[`U_T${slot}`] = mapped.type
    }
  }

  if (gstTerm) {
    const mapped = applyPaymentPercentToTerm(
      gstTerm,
      resolvePaymentTermPercent(gstTerm) ?? gstTerm.gst,
      gstTerm.type,
    )
    const slot = GST_PAYMENT_TERM_SLOT
    next[`U_G${slot}`] = mapped.gst ?? 0
    // U_B11 does not exist on this company DB — never write it.
    if (mapped.desc) next[`U_D${slot}`] = mapped.desc
    if (mapped.stage) next[`U_S${slot}`] = mapped.stage
    if (mapped.type) next[`U_T${slot}`] = mapped.type
  } else {
    next[`U_G${GST_PAYMENT_TERM_SLOT}`] = 0
  }

  return next
}

/** Next free slot for a new term. GST types always use slot 11 (one only). */
export function nextPaymentTermSlot(
  existing: PaymentTermRow[],
  type?: string | null,
): number | null {
  if (isGstPaymentTermType(type)) {
    if (hasGstPaymentTerm(existing)) return null
    return GST_PAYMENT_TERM_SLOT
  }

  const used = new Set(
    existing
      .filter((t) => !isGstPaymentTermType(t.type) && t.id !== GST_PAYMENT_TERM_SLOT)
      .map((t) => t.id),
  )
  for (let i = 1; i <= MAX_BASIC_PAYMENT_TERMS; i += 1) {
    if (!used.has(i)) return i
  }
  return null
}

export function readLogisticsFromPo(po: PoRecord): PurchaseOrderLogistics {
  return {
    // ADOC U_CardCode is the real Dispatch To BP field; U_DispatchTo is not valid on this DB.
    dispatchTo: readString(po, 'U_CardCode', 'U_DispatchTo'),
    dispatchAddress: readString(po, 'U_DispachAdd'),
    contactPerson: readString(po, 'U_ContactPerson'),
    // Real SAP UDFs: U_PRI_BAS / U_TransMode (legacy invented names kept as read fallbacks only).
    priceBasis: readString(po, 'U_PRI_BAS', 'U_PriceBasis'),
    modeOfTransport: readString(po, 'U_TransMode', 'U_ModeOfTransport'),
  }
}

export function applyLogisticsToPo(po: PoRecord, logistics: PurchaseOrderLogistics): PoRecord {
  const next: PoRecord = {
    ...po,
    U_CardCode: logistics.dispatchTo || undefined,
    U_DispachAdd: logistics.dispatchAddress || undefined,
    U_ContactPerson: logistics.contactPerson || undefined,
    U_PRI_BAS: logistics.priceBasis || undefined,
    U_TransMode: logistics.modeOfTransport || undefined,
    // Do not map BP CardCode onto ShipToCode (ShipToCode is an address name on the vendor).
    U_DispatchTo: undefined,
    U_PriceBasis: undefined,
    U_ModeOfTransport: undefined,
    ShipToCode: undefined,
  }
  // Header warehouse is line-default UI only — U_Warehouse is not a valid OPOR UDF on this DB.
  delete next.U_Warehouse
  delete next.ShipToCode
  return next
}

/** Format a SAP BPAddresses row for U_DispachAdd (max 120 chars). */
export function formatBpDispatchAddress(address: {
  AddressName?: string | null
  AddressName2?: string | null
  AddressName3?: string | null
  Street?: string | null
  StreetNo?: string | null
  BuildingFloorRoom?: string | null
  Block?: string | null
  City?: string | null
  State?: string | null
  ZipCode?: string | null
  Country?: string | null
}): string {
  const parts = [
    address.AddressName2,
    address.AddressName3,
    address.Street,
    address.StreetNo,
    address.BuildingFloorRoom,
    address.Block,
    address.City,
    address.State,
    address.ZipCode,
    address.Country,
  ]
    .map((p) => (p ?? '').trim())
    .filter(Boolean)
  const formatted = parts.join(', ')
  if (formatted) return formatted.slice(0, 120)
  return (address.AddressName ?? '').trim().slice(0, 120)
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

export function paymentTermDisplayLabel(
  term: PaymentTermRow,
  typeLabels?: Record<string, string>,
): string {
  if (term.desc) return term.desc
  const typeKey = normalizePaymentTermType(term.type)
  const typeLabel = (typeKey && typeLabels?.[typeKey]) || term.type
  const percent = resolvePaymentTermPercent(term)
  const parts = [
    typeLabel,
    percent != null ? `${percent}%` : '',
    term.stage,
  ].filter(Boolean)
  return parts.join(' · ') || `Term ${term.id}`
}
