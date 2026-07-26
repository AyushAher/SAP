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

export interface PurchaseOrderOtherTerms {
  deliveryTerms?: string
  inspectionBy?: string
  transportation?: string
  supervision?: string
  transitInsurance?: string
  drawingDocuments?: string
  loading?: string
  warranty?: string
  unloading?: string
  otherRemark?: string
  painting?: string
  testCertificates?: string
}

export interface PurchaseOrderLogistics {
  dispatchTo?: string
  contactPerson?: string
  priceBasis?: string
  modeOfTransport?: string
  materialOutwardDoc?: string
  goodsIssueTransfer?: string
  materialInwardDoc?: string
  goodsReceiptTransfer?: string
}

export const PAYMENT_TERM_TYPE_OPTIONS = [
  { value: 'Advance', label: 'Advance' },
  { value: 'Running', label: 'Running' },
  { value: 'Invoice', label: 'Invoice' },
  { value: 'Retention', label: 'Retention' },
] as const

export const MAX_PAYMENT_TERMS = 11
