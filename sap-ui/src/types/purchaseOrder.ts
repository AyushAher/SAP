import type { DocumentLineItem } from '@/types/production'

export interface PurchaseOrderLineItem extends DocumentLineItem {
  UomName?: string
  /** Purchase UoM (sent to SAP as UoMCode). */
  UoMCode?: string
  UoMEntry?: number
  /** Stock / inventory UoM (from item master; display). */
  StockUom?: string
  /** Stock / inventory quantity. ItemsPerUnit = StockQty / PurchaseQty. */
  StockQty?: number
  /**
   * SAP UnitsOfMeasurment (NumPerMsr) — items per purchase unit.
   * Computed as StockQty / Quantity (purchase qty).
   */
  UnitsOfMeasurment?: number
  /**
   * SAP UseBaseUnits — Inventory UoM Yes/No.
   * tYES when Items per Unit is 1, otherwise tNO.
   */
  UseBaseUnits?: string
  WeightKg?: number
  TaxableAmount?: number
  DiscountPercent?: number
  /** India GST AbsEntry (OCHP). */
  HSNEntry?: number
  /** Display label for HSN (not sent to SAP). */
  HsnLabel?: string
  /** India GST SAC AbsEntry. */
  SACEntry?: number
  SacLabel?: string
  /** G/L account — required on service document lines. */
  AccountCode?: string
  AccountLabel?: string
  ProjectCode?: string
  CostingCode?: string
  CostingCode2?: string
  CostingCode3?: string
  CostingCode4?: string
  CostingCode5?: string
  /** Production order no. — required when header U_PO_Type = JOB. */
  U_ProdNo?: string
}

export interface PaymentTermRow {
  id: number
  type?: string
  basic?: number
  gst?: number
  stage?: string
  desc?: string
}

/** Other Terms tab — maps to OPOR UDFs (see purchaseOrderForm helpers). */
export interface PurchaseOrderOtherTerms {
  /** U_DL */
  deliveryTerms?: string
  /** U_INSPBY */
  inspectionBy?: string
  /** U_TRANS */
  transportation?: string
  /** U_SUPR */
  supervision?: string
  /** U_TRANINSU */
  transitInsurance?: string
  /** U_DRA_DOC */
  drawingDocuments?: string
  /** U_LOAD */
  loading?: string
  /** U_WARR */
  warranty?: string
  /** U_UN_LOAD */
  unloading?: string
  /** U_ANOTHREM */
  otherRemark?: string
  /** U_PAIN */
  painting?: string
  /** U_TC */
  testCertificates?: string
}

export interface PurchaseOrderLogistics {
  /** Business partner CardCode — maps to ADOC U_CardCode (Dispatch To / Ship To). */
  dispatchTo?: string
  /** SAP UDF U_DispachAdd (note SAP spelling). Max 120 chars. */
  dispatchAddress?: string
  /** SAP UDF U_ContactPerson — contact name from the Dispatch To BP. */
  contactPerson?: string
  priceBasis?: string
  modeOfTransport?: string
}

/** SAP U_PriceBasis UDF — no master list in codebase; common incoterms-style values. */
export const PRICE_BASIS_OPTIONS = [
  { value: 'EXW', label: 'EXW — Ex Works' },
  { value: 'FOB', label: 'FOB — Free On Board' },
  { value: 'CIF', label: 'CIF — Cost, Insurance and Freight' },
  { value: 'CFR', label: 'CFR — Cost and Freight' },
  { value: 'FOR', label: 'FOR — Free On Rail' },
  { value: 'FAS', label: 'FAS — Free Alongside Ship' },
  { value: 'DDP', label: 'DDP — Delivered Duty Paid' },
  { value: 'DAP', label: 'DAP — Delivered At Place' },
] as const

/** SAP U_ModeOfTransport UDF — no master list in codebase; common transport modes. */
export const MODE_OF_TRANSPORT_OPTIONS = [
  { value: 'Road', label: 'Road' },
  { value: 'Rail', label: 'Rail' },
  { value: 'Sea', label: 'Sea' },
  { value: 'Air', label: 'Air' },
  { value: 'Courier', label: 'Courier' },
  { value: 'Pipeline', label: 'Pipeline' },
  { value: 'Multimodal', label: 'Multimodal' },
] as const

/** Types that store Payment% in U_Gn (GST) rather than U_Bn (Basic). */
export const GST_PAYMENT_TERM_TYPES = ['GstProforma', 'TaxInvoice'] as const

export type GstPaymentTermType = (typeof GST_PAYMENT_TERM_TYPES)[number]

/**
 * Fallback when GET /masters/payment-term-types fails.
 * Values match SAP OPOR T1 ValidValues (+ app extras). Legacy UI "Running" maps to Proforma.
 */
export const PAYMENT_TERM_TYPE_OPTIONS = [
  { value: 'Advance', label: 'As Advance' },
  { value: 'Proforma', label: 'Against Proforma' },
  { value: 'Invoice', label: 'Against Invoice' },
  { value: 'Retention', label: 'Retention' },
  { value: 'GstProforma', label: 'GST against Proforma Invoice' },
  { value: 'TaxInvoice', label: 'Against Tax Invoice' },
] as const

export const MAX_PAYMENT_TERMS = 11
