export interface ProductionOrderLine {
  LineNumber?: number
  VisualOrder?: number
  ItemNo?: string
  ItemName?: string
  PlannedQuantity?: number
  IssuedQuantity?: number
  Warehouse?: string
  DocumentAbsoluteEntry?: number
  ProductionOrderIssueType?: string
  Project?: string
  LocationCode?: number
  BaseQuantity?: number
}

/**
 * Editable view of a production order, in the friendly names the UI uses. Bodies sent to the API
 * must be built with `toProductionOrderPayload`, which translates these into the SAP names the
 * request model binds.
 */
export interface ProductionOrder {
  AbsoluteEntry?: number
  DocumentNumber?: number
  ItemNumber?: string
  ProductDescription?: string
  CustomerCode?: string
  CustomerName?: string
  Project?: string
  ProjectName?: string
  Warehouse?: string
  DrawingNo?: string
  Status?: string
  CreationDate?: string
  PlannedQuantity?: number
  CompletedQuantity?: number
  RejectedQuantity?: number
  Priority?: number
  UoMEntry?: number
  ProductionOrderLines?: ProductionOrderLine[]
  SalesOrderDocNum?: number
  SalesOrderDocEntry?: number
  Type?: string
  ProductionCategory?: string
  /** UI-only: seeds the warehouse of every component line. Never reaches SAP. */
  IssWarehouse?: string
  PostingDate?: string
  DueDate?: string
  StartDate?: string
  Remarks?: string
  [key: string]: unknown
}

export interface ProductionOrderSelection {
  ProductionOrder: ProductionOrder
  ProductionOrderLinesEntryNumber: ProductionOrderLine[]
  WorkerName?: string
}

export interface ProductionOrderAddLineResult {
  AddedLine: ProductionOrderLine
  ProductionOrder?: ProductionOrder
}

export interface DocumentLineItem {
  ItemCode?: string
  ItemDescription?: string
  Quantity?: number
  UnitPrice?: number
  TaxCode?: string
  WarehouseCode?: string
  FromWarehouseCode?: string
  LineTotal?: number
  TaxTotal?: number
  GrossTotal?: number
  LineNum?: number
}
