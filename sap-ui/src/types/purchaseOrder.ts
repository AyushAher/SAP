import type { DocumentLineItem } from '@/types/production'

export interface PurchaseOrderLineItem extends DocumentLineItem {
  UomName?: string
  StockQty?: number
  WeightKg?: number
  TaxableAmount?: number
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
