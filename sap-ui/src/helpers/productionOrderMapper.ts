import type { ProductionOrder, ProductionOrderLine, ProductionOrderSelection } from '@/types/production'

const RELEASED_STATUS = 'boposReleased'

function readString(raw: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = raw[key]
    if (value !== undefined && value !== null && value !== '') return String(value)
  }
  return undefined
}

function readNumber(raw: Record<string, unknown>, ...keys: string[]): number | undefined {
  for (const key of keys) {
    const value = raw[key]
    if (value !== undefined && value !== null && value !== '') return Number(value)
  }
  return undefined
}

/**
 * Read direction. The result holds exactly one representation of every header field, under the
 * friendly name the UI uses; the raw SAP-named members are deliberately not spread in. Carrying
 * both spellings let a dropdown update `Status` while the outgoing body was still built from
 * `ProductionOrderStatus`, so an edit could return 200 and change nothing in SAP.
 * The write direction is {@link toProductionOrderPayload}.
 */
export function normalizeProductionOrder(raw: ProductionOrder | Record<string, unknown>): ProductionOrder {
  const source = (raw ?? {}) as Record<string, unknown>
  const rawLines = source.ProductionOrderLines ?? source.productionOrderLines
  const productionOrderLines = Array.isArray(rawLines)
    ? rawLines.map((line) => normalizeProductionOrderLine(line as ProductionOrderLine))
    : undefined

  return {
    AbsoluteEntry: readNumber(source, 'AbsoluteEntry', 'absoluteEntry'),
    DocumentNumber: readNumber(source, 'DocumentNumber', 'documentNumber'),
    ItemNumber: readString(source, 'ItemNumber', 'ItemNo', 'itemNo', 'itemNumber'),
    Status: readString(source, 'Status', 'ProductionOrderStatus', 'productionOrderStatus', 'status'),
    Type: readString(source, 'Type', 'ProductionOrderType', 'productionOrderType'),
    ProductionCategory: readString(source, 'ProductionCategory', 'U_ProdType', 'u_ProdType'),
    ProductDescription: readString(source, 'ProductDescription', 'productDescription'),
    CustomerCode: readString(source, 'CustomerCode', 'customerCode'),
    CustomerName: readString(source, 'CustomerName', 'customerName', 'U_CustomerName', 'u_CustomerName'),
    Project: readString(source, 'Project', 'project'),
    ProjectName: readString(source, 'ProjectName', 'projectName', 'U_PrjName', 'u_PrjName'),
    Warehouse: readString(source, 'Warehouse', 'warehouse'),
    DrawingNo: readString(source, 'DrawingNo', 'drawingNo', 'U_DwgNo', 'u_DwgNo'),
    Remarks: readString(source, 'Remarks', 'remarks'),
    SalesOrderDocNum: readNumber(source, 'SalesOrderDocNum', 'ProductionOrderOriginNumber', 'productionOrderOriginNumber'),
    SalesOrderDocEntry: readNumber(source, 'SalesOrderDocEntry', 'ProductionOrderOriginEntry', 'productionOrderOriginEntry'),
    PlannedQuantity: readNumber(source, 'PlannedQuantity', 'plannedQuantity'),
    CompletedQuantity: readNumber(source, 'CompletedQuantity', 'completedQuantity'),
    RejectedQuantity: readNumber(source, 'RejectedQuantity', 'rejectedQuantity'),
    Priority: readNumber(source, 'Priority', 'priority'),
    UoMEntry: readNumber(source, 'UoMEntry', 'uoMEntry'),
    PostingDate: readString(source, 'PostingDate', 'postingDate'),
    DueDate: readString(source, 'DueDate', 'dueDate'),
    StartDate: readString(source, 'StartDate', 'startDate'),
    CreationDate: readString(source, 'CreationDate', 'creationDate'),
    ProductionOrderLines: productionOrderLines,
  }
}

/**
 * Lines are passed through rather than rebuilt: every line property the UI touches already has
 * the same name on both sides, so nothing can shadow, and a SAP update replaces the whole line
 * collection — dropping fields we do not model (UoMCode, U_FreeTxt, U_DocNum) would blank them.
 */
export function normalizeProductionOrderLine(raw: ProductionOrderLine | Record<string, unknown>): ProductionOrderLine {
  const source = raw as Record<string, unknown>
  return {
    ...(raw as ProductionOrderLine),
    LineNumber: readNumber(source, 'LineNumber', 'lineNumber'),
    VisualOrder: readNumber(source, 'VisualOrder', 'visualOrder'),
    ItemNo: readString(source, 'ItemNo', 'itemNo'),
    ItemName: readString(source, 'ItemName', 'itemName'),
    PlannedQuantity: readNumber(source, 'PlannedQuantity', 'plannedQuantity'),
    IssuedQuantity: readNumber(source, 'IssuedQuantity', 'issuedQuantity'),
    Warehouse: readString(source, 'Warehouse', 'warehouse'),
    DocumentAbsoluteEntry: readNumber(source, 'DocumentAbsoluteEntry', 'documentAbsoluteEntry'),
  }
}

/**
 * Write direction, mirroring {@link normalizeProductionOrder}. The API request model binds SAP
 * names (`ItemNo`, `ProductionOrderStatus`, `U_ProdType`, …) through `[JsonPropertyName]`, which
 * also governs deserialisation, so a member named `ItemNumber` matches nothing and is dropped in
 * transit. Every production order body the UI sends must be built here.
 *
 * Optional user fields are omitted when empty rather than sent as blanks: SAP leaves `U_ProdType`
 * and `U_DwgNo` null on many real orders, and a Service Layer update only touches the properties
 * present in the body.
 */
export function toProductionOrderPayload(
  order: ProductionOrder,
  lines?: ProductionOrderLine[],
): Record<string, unknown> {
  const payload: Record<string, unknown> = {}
  const set = (key: string, value: string | number | undefined | null) => {
    if (value === undefined || value === null || value === '') return
    payload[key] = value
  }

  set('AbsoluteEntry', order.AbsoluteEntry)
  set('DocumentNumber', order.DocumentNumber)
  set('ItemNo', order.ItemNumber)
  set('ProductionOrderStatus', order.Status)
  set('ProductionOrderType', order.Type)
  set('U_ProdType', order.ProductionCategory)
  set('U_DwgNo', order.DrawingNo)
  set('ProductDescription', order.ProductDescription)
  set('CustomerCode', order.CustomerCode)
  // U_CustomerName does not exist on OWOR; the API drops it before calling SAP, but the issue and
  // receipt drafts persist it, so it stays in the body the API receives.
  set('U_CustomerName', order.CustomerName)
  set('Project', order.Project)
  set('U_PrjName', order.ProjectName)
  set('Warehouse', order.Warehouse)
  set('Remarks', order.Remarks)
  set('ProductionOrderOriginNumber', order.SalesOrderDocNum)
  set('ProductionOrderOriginEntry', order.SalesOrderDocEntry)
  set('DueDate', order.DueDate)
  set('StartDate', order.StartDate)
  set('CreationDate', order.CreationDate)
  set('UoMEntry', order.UoMEntry)
  set('Priority', order.Priority)

  // Non-nullable on the request model: leaving them out makes the API send 0 or 0001-01-01 to SAP
  // instead of what the order already holds.
  payload.PlannedQuantity = order.PlannedQuantity ?? 0
  payload.CompletedQuantity = order.CompletedQuantity ?? 0
  payload.RejectedQuantity = order.RejectedQuantity ?? 0
  payload.PostingDate = order.PostingDate ?? new Date().toISOString().slice(0, 10)

  payload.ProductionOrderLines = (lines ?? order.ProductionOrderLines ?? []).map((line) => ({ ...line }))

  return payload
}

export function isReleasedProductionOrder(order: ProductionOrder): boolean {
  const normalized = normalizeProductionOrder(order)
  return normalized.Status === RELEASED_STATUS
}

export function normalizeProductionOrders(rows: ProductionOrder[] | undefined): ProductionOrder[] {
  return (rows ?? []).map(normalizeProductionOrder)
}

export function normalizeProductionOrderSelection(raw: unknown): ProductionOrderSelection {
  const source = (raw ?? {}) as Record<string, unknown>
  const orderRaw = source.ProductionOrder ?? source.productionOrder
  const linesRaw = source.ProductionOrderLinesEntryNumber ?? source.productionOrderLinesEntryNumber ?? []

  return {
    ProductionOrder: normalizeProductionOrder(orderRaw as ProductionOrder),
    ProductionOrderLinesEntryNumber: Array.isArray(linesRaw)
      ? linesRaw.map((line) => normalizeProductionOrderLine(line as ProductionOrderLine))
      : [],
    WorkerName: readString(source, 'WorkerName', 'workerName'),
  }
}

/**
 * Issue / receipt drafts are stored as the request body they were saved with, and the API reads
 * SAP-named members out of it, so the selected order has to be translated back on the way out.
 */
export function toProductionOrderSelectionPayload(selection: ProductionOrderSelection): Record<string, unknown> {
  return {
    ProductionOrder: toProductionOrderPayload(selection.ProductionOrder),
    ProductionOrderLinesEntryNumber: (selection.ProductionOrderLinesEntryNumber ?? []).map((line) => ({ ...line })),
    WorkerName: selection.WorkerName,
  }
}
