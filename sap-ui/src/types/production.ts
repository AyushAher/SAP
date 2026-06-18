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
  ProductionOrderLines?: ProductionOrderLine[]
  SalesOrderDocNum?: number
  SalesOrderDocEntry?: number
  Type?: string
  ProductionCategory?: string
  IssWarehouse?: string
  PostingDate?: string
  Remarks?: string
  [key: string]: unknown
}

export interface ProductionOrderSelection {
  ProductionOrder: ProductionOrder
  ProductionOrderLinesEntryNumber: ProductionOrderLine[]
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
