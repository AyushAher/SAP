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
  /** SAP UDF U_PRI_BAS ValidValues. */
  priceBasis?: string
  /** SAP UDF U_TransMode ValidValues (-,1,2,3,4). */
  modeOfTransport?: string
}

/** Fallback when GET /masters/purchase-order-logistics-options fails — match SAP U_PRI_BAS. */
export const PRICE_BASIS_OPTIONS = [
  { value: 'ex works(incoterms)', label: 'ex works(incoterms)' },
  { value: 'F.O.R.', label: 'F.O.R.' },
  { value: 'NOT APPLIC', label: 'NOT APPLICABLE' },
] as const

/** Fallback when logistics options API fails — match SAP U_TransMode. */
export const MODE_OF_TRANSPORT_OPTIONS = [
  { value: '-', label: 'Not Applicable' },
  { value: '1', label: 'Road' },
  { value: '2', label: 'Rail' },
  { value: '3', label: 'Air' },
  { value: '4', label: 'Ship' },
] as const

/** Types that store Payment% in U_G11 (GST) rather than U_Bn (Basic). */
export const GST_PAYMENT_TERM_TYPES = ['GstProforma', 'TaxInvoice'] as const

export type GstPaymentTermType = (typeof GST_PAYMENT_TERM_TYPES)[number]

/** Fixed OPOR slot for GST payment % — only U_G11 (no U_B11 on this company DB). */
export const GST_PAYMENT_TERM_SLOT = 11

/** Non-GST payment terms use slots 1–10 (U_Bn). */
export const MAX_BASIC_PAYMENT_TERMS = 10

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

/** Total slots including GST slot 11. */
export const MAX_PAYMENT_TERMS = GST_PAYMENT_TERM_SLOT
