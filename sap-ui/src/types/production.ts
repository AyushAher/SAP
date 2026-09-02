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
  /** WOR1 U_DocNum — sub-assembly tag on a parent production order line. */
  DocNum?: string
  /** WOR1 U_DwgNo. */
  DrawingNo?: string
  /** WOR1 U_FreeTxt. Drawing name is the default when this is empty. */
  FreeText?: string
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
  /**
   * Sub-assembly number stored on OWOR U_DocNum: `{parentDocNum}/{sequence}` (legacy rows
   * may store only the parent DocumentNumber).
   */
  ParentProductionOrderNo?: string
  /** SAP AbsoluteEntry of the parent production order for a virtual sub-assembly. */
  ParentAbsoluteEntry?: number
  /** Local key for a sub-assembly drafted before the parent exists in SAP. */
  DraftKey?: string
  /** Portal-only: sub-assemblies sent with a new parent so SAP is created once. */
  Subassemblies?: ProductionOrder[]
  Weight?: number
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

/** A finished-good row from the linked sales order, used to cap Product No. and Planned Qty. */
export interface SalesOrderProductLine {
  ItemCode?: string
  ItemName?: string
  Quantity?: number
  UnitsOfMeasurment?: number
  InventoryQuantity?: number
  LineNum?: number
}

/** SAP ProductionOrderType. The UI no longer offers Type; every order is Special. */
export const PRODUCTION_ORDER_TYPE_SPECIAL = 'bopotSpecial'

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
  LocationCode?: number
  FromWarehouseCode?: string
  LineTotal?: number
  TaxTotal?: number
  GrossTotal?: number
  LineNum?: number
}
