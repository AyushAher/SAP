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

/** GST-dedicated types store Payment% in U_G11; other types default to U_Bn unless Basis=GST. */
export function isGstPaymentTermType(type?: string | null): boolean {
  const normalized = normalizePaymentTermType(type)
  if (!normalized) return false
  return (GST_PAYMENT_TERM_TYPES as readonly string[]).some(
    (t) => t.localeCompare(normalized, undefined, { sensitivity: 'accent' }) === 0,
  )
}

/** True when this row's Payment% belongs on U_G11 (not U_Bn). */
export function isGstPaymentTermRow(
  term: Pick<PaymentTermRow, 'id' | 'type' | 'basic' | 'gst'>,
): boolean {
  if (term.id === GST_PAYMENT_TERM_SLOT) return true
  if (isGstPaymentTermType(term.type)) return true
  const gst = term.gst != null && Number.isFinite(term.gst) && term.gst > 0
  const basic = term.basic != null && Number.isFinite(term.basic) && term.basic > 0
  // Legacy / Basis=GST rows: percent lives in gst (e.g. Invoice + 100% GST on old U_G3).
  return gst && !basic
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
 * GST rows → gst; else basic if set; else gst (legacy mixed rows).
 */
export function resolvePaymentTermPercent(term: Pick<PaymentTermRow, 'id' | 'type' | 'basic' | 'gst'>): number | undefined {
  const basic = term.basic != null && Number.isFinite(term.basic) && term.basic > 0 ? term.basic : undefined
  const gst = term.gst != null && Number.isFinite(term.gst) && term.gst > 0 ? term.gst : undefined
  if (isGstPaymentTermRow(term) && gst != null) return gst
  if (isGstPaymentTermRow(term) && gst == null && basic != null) return basic
  if (basic != null) return basic
  if (gst != null) return gst
  return undefined
}

export type PaymentPercentBasis = 'basic' | 'gst'

/** Split a Payment% into basic/gst fields (clears the unused field). */
export function applyPaymentPercentToTerm<T extends Pick<PaymentTermRow, 'type' | 'basic' | 'gst'>>(
  term: T,
  percent: number | undefined,
  type: string | undefined = term.type,
  basis?: PaymentPercentBasis,
): T {
  const normalizedType = normalizePaymentTermType(type) || undefined
  const asGst = basis === 'gst' || (basis !== 'basic' && isGstPaymentTermType(normalizedType))
  if (asGst) {
    return { ...term, type: normalizedType, basic: undefined, gst: percent }
  }
  return { ...term, type: normalizedType, basic: percent, gst: undefined }
}

export function hasGstPaymentTerm(terms: PaymentTermRow[]): boolean {
  return terms.some((t) => isGstPaymentTermRow(t))
}

/**
 * Parse OPOR payment-term UDFs.
 * Any U_G{n}>0 (n=1–11) coalesces into the single U_G11 GST term.
 * If a slot also has Basic%, keep the basic row on that slot.
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
    const basicValue = basic != null && basic > 0 ? basic : basic === 0 ? 0 : undefined
    const hasBasic = basicValue != null && basicValue > 0

    // Any positive GST% on slots 1–11 belongs on the single G11 term (incl. legacy Invoice on U_G3).
    if (isGstSlot || isGstType || gstValue != null) {
      if (!gstTerm || isGstSlot || (gstValue != null && gstTerm.gst == null)) {
        gstTerm = {
          id: GST_PAYMENT_TERM_SLOT,
          type: (isGstSlot || isGstType || !hasBasic ? type : undefined) || gstTerm?.type,
          basic: undefined,
          gst: gstValue ?? gstTerm?.gst,
          stage: (isGstSlot || isGstType || !hasBasic ? stage : undefined) || gstTerm?.stage,
          desc: (isGstSlot || isGstType || !hasBasic ? desc : undefined) || gstTerm?.desc,
        }
      }
      // Dual-field legacy: keep Basic% on the original slot; GST moved to 11.
      if (!isGstSlot && !isGstType && hasBasic) {
        terms.push({
          id: i,
          type,
          basic: basicValue,
          gst: undefined,
          stage: stage || undefined,
          desc: desc || undefined,
        })
      }
      continue
    }

    if (type || basic != null || stage || desc) {
      terms.push({
        id: i,
        type,
        basic: basicValue,
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

export function applyPaymentTermsToPo(
  po: PoRecord,
  terms: PaymentTermRow[],
  typeLabels?: Record<string, string>,
): PoRecord {
  const next = { ...po }
  for (let i = 1; i <= MAX_PAYMENT_TERMS; i += 1) {
    delete next[`U_B${i}`]
    delete next[`U_G${i}`]
    delete next[`U_D${i}`]
    delete next[`U_S${i}`]
    delete next[`U_T${i}`]
    // Defensive: API camelCase clones must not leak legacy UGst3 into SAP.
    delete next[`UGst${i}`]
    delete next[`UBasic${i}`]
    delete next[`UType${i}`]
    delete next[`UStage${i}`]
    delete next[`UDes${i}`]
  }

  // Always clear GST% on slots 1–10 — only U_G11 may hold GST.
  for (let i = 1; i <= MAX_BASIC_PAYMENT_TERMS; i += 1) {
    next[`U_G${i}`] = 0
  }

  const basicTerms = terms.filter((t) => !isGstPaymentTermRow(t))
  const gstTerm = terms.find((t) => isGstPaymentTermRow(t))

  for (const term of basicTerms.slice(0, MAX_BASIC_PAYMENT_TERMS)) {
    const slot = Math.min(Math.max(term.id, 1), MAX_BASIC_PAYMENT_TERMS)
    const percent = resolvePaymentTermPercent(term) ?? term.basic
    const desc = buildPaymentTermDescription({ ...term, id: slot }, typeLabels)
    if (term.type || percent != null || term.stage || desc) {
      next[`U_B${slot}`] = percent ?? 0
      next[`U_G${slot}`] = 0
      if (desc) next[`U_D${slot}`] = desc
      if (term.stage) next[`U_S${slot}`] = term.stage
      if (term.type) next[`U_T${slot}`] = normalizePaymentTermType(term.type) || term.type
    }
  }

  if (gstTerm) {
    const percent = resolvePaymentTermPercent(gstTerm) ?? gstTerm.gst ?? 0
    const slot = GST_PAYMENT_TERM_SLOT
    // Always write GST% to U_G11 — even when type is Invoice / Retention (legacy habit).
    next[`U_G${slot}`] = percent
    // U_B11 does not exist on this company DB — never write it.
    const desc = buildPaymentTermDescription({ ...gstTerm, id: slot }, typeLabels)
    if (desc) next[`U_D${slot}`] = desc
    if (gstTerm.stage) next[`U_S${slot}`] = gstTerm.stage
    if (gstTerm.type) next[`U_T${slot}`] = normalizePaymentTermType(gstTerm.type) || gstTerm.type
  } else {
    next[`U_G${GST_PAYMENT_TERM_SLOT}`] = 0
  }

  return next
}

/** Next free slot for a new term. GST rows always use slot 11 (one only). */
export function nextPaymentTermSlot(
  existing: PaymentTermRow[],
  type?: string | null,
  basis?: PaymentPercentBasis,
): number | null {
  const asGst = basis === 'gst' || (basis !== 'basic' && isGstPaymentTermType(type))
  if (asGst) {
    if (hasGstPaymentTerm(existing)) return null
    return GST_PAYMENT_TERM_SLOT
  }

  const used = new Set(
    existing
      .filter((t) => !isGstPaymentTermRow(t))
      .map((t) => t.id),
  )
  for (let i = 1; i <= MAX_BASIC_PAYMENT_TERMS; i += 1) {
    if (!used.has(i)) return i
  }
  return null
}

export function readLogisticsFromPo(po: PoRecord): PurchaseOrderLogistics {
  return {
    // U_DisID is the Dispatch To BP field; U_CardCode is the fallback for POs saved before that move.
    dispatchTo: readString(po, 'U_DisID', 'U_CardCode', 'U_DispatchTo'),
    dispatchAddress: readString(po, 'U_DispachAdd'),
    // Contact Person is an employee (+ phone) stored in U_SHIPTO.
    contactPerson: readString(po, 'U_SHIPTO', 'U_ContactPerson'),
    // Real SAP UDFs: U_PRI_BAS / U_TransMode (legacy invented names kept as read fallbacks only).
    priceBasis: readString(po, 'U_PRI_BAS', 'U_PriceBasis'),
    modeOfTransport: readString(po, 'U_TransMode', 'U_ModeOfTransport'),
  }
}

export function applyLogisticsToPo(po: PoRecord, logistics: PurchaseOrderLogistics): PoRecord {
  const next: PoRecord = {
    ...po,
    U_DisID: logistics.dispatchTo || undefined,
    U_DispachAdd: logistics.dispatchAddress?.slice(0, 120) || undefined,
    U_SHIPTO: logistics.contactPerson || undefined,
    U_PRI_BAS: logistics.priceBasis || undefined,
    U_TransMode: logistics.modeOfTransport || undefined,
    // Do not map BP CardCode onto ShipToCode (ShipToCode is an address name on the vendor).
    // U_CardCode is no longer written — a stale value loaded from SAP must not be echoed back.
    U_CardCode: undefined,
    U_DispatchTo: undefined,
    U_ContactPerson: undefined,
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

/**
 * PBBPL branch: Dispatch Location (type of location) → warehouse.
 * Factory → Store1, Office → Store5, BP Loc → PBPL(S).
 */
export const PO_DISPATCH_LOCATION_OPTIONS = [
  { value: 'Factory', label: 'Factory' },
  { value: 'Office', label: 'Office' },
  { value: 'BP Loc', label: 'BP Loc' },
] as const

export type PoDispatchLocation = (typeof PO_DISPATCH_LOCATION_OPTIONS)[number]['value']

const PO_DISPATCH_LOCATION_TO_WAREHOUSE: Record<PoDispatchLocation, string> = {
  Factory: 'Store1',
  Office: 'Store5',
  'BP Loc': 'PBPL(S)',
}

const PO_WAREHOUSE_TO_DISPATCH_LOCATION: Record<string, PoDispatchLocation> = {
  Store1: 'Factory',
  STORE1: 'Factory',
  Store5: 'Office',
  STORE5: 'Office',
  'PBPL(S)': 'BP Loc',
}

/** True for PBBPL company DBs (UAT/LIVE) — warehouse↔location mapping applies. */
export function usesPbbplDispatchLocationMapping(companyDb?: string | null): boolean {
  const db = (companyDb ?? '').trim().toUpperCase()
  return db.startsWith('PBBPL')
}

export function warehouseForDispatchLocation(location?: string | null): string | undefined {
  const key = (location ?? '').trim() as PoDispatchLocation
  return PO_DISPATCH_LOCATION_TO_WAREHOUSE[key]
}

export function dispatchLocationForWarehouse(warehouse?: string | null): PoDispatchLocation | undefined {
  const code = (warehouse ?? '').trim()
  if (!code) return undefined
  return PO_WAREHOUSE_TO_DISPATCH_LOCATION[code]
    ?? PO_WAREHOUSE_TO_DISPATCH_LOCATION[code.toUpperCase()]
}

/** Type wording used by existing OPOR descriptions; live SAP ValidValue descriptions win. */
const PAYMENT_TERM_TYPE_PHRASES: Record<string, string> = {
  Advance: 'As Advance',
  Proforma: 'Against Proforma',
  Invoice: 'Against Invoice',
  Retention: 'Retention',
  GstProforma: 'GST against Proforma Invoice',
  TaxInvoice: 'Against Tax Invoice',
}

function paymentTermTypePhrase(
  type: string,
  isGst: boolean,
  typeLabels?: Record<string, string>,
): string {
  const phrase = (typeLabels?.[type] || PAYMENT_TERM_TYPE_PHRASES[type] || type).trim()
  // "100% GST GST against Proforma Invoice" reads badly — the basis already says GST.
  if (isGst && /^gst\s+/i.test(phrase)) return phrase.slice(4).trim()
  return phrase
}

/**
 * SAP U_Dn payment-term description:
 * `{Value}% {Basic|GST} {Type} {Stage}`
 * e.g. `20% Basic As Advance Stage1`, `100% GST Against Invoice`
 */
export function buildPaymentTermDescription(
  term: Pick<PaymentTermRow, 'id' | 'type' | 'basic' | 'gst' | 'stage'>,
  typeLabels?: Record<string, string>,
): string {
  const percent = resolvePaymentTermPercent(term)
  const isGst = isGstPaymentTermRow(term)
  const type = normalizePaymentTermType(term.type)
  const stage = (term.stage ?? '').trim()
  return [
    percent != null && Number.isFinite(percent) ? `${percent}%` : '',
    isGst ? 'GST' : 'Basic',
    type ? paymentTermTypePhrase(type, isGst, typeLabels) : '',
    stage,
  ].filter(Boolean).join(' ')
}

export function paymentTermDisplayLabel(
  term: PaymentTermRow,
  typeLabels?: Record<string, string>,
): string {
  const built = buildPaymentTermDescription(term, typeLabels)
  if (built) {
    // Prefer live structure over a stale stored desc when type/percent/stage are present.
    if (term.type || resolvePaymentTermPercent(term) != null || term.stage) return built
  }
  if (term.desc) return term.desc
  const typeKey = normalizePaymentTermType(term.type)
  const typeLabel = (typeKey && typeLabels?.[typeKey]) || term.type
  return typeLabel || `Term ${term.id}`
}
