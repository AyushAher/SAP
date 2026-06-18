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

export function normalizeProductionOrder(raw: ProductionOrder | Record<string, unknown>): ProductionOrder {
  const source = (raw ?? {}) as Record<string, unknown>
  const rawLines = source.ProductionOrderLines ?? source.productionOrderLines
  const productionOrderLines = Array.isArray(rawLines)
    ? rawLines.map((line) => normalizeProductionOrderLine(line as ProductionOrderLine))
    : undefined

  return {
    ...(raw as ProductionOrder),
    AbsoluteEntry: readNumber(source, 'AbsoluteEntry', 'absoluteEntry'),
    DocumentNumber: readNumber(source, 'DocumentNumber', 'documentNumber'),
    ItemNumber: readString(source, 'ItemNumber', 'ItemNo', 'itemNo', 'itemNumber'),
    Status: readString(source, 'Status', 'ProductionOrderStatus', 'productionOrderStatus', 'status'),
    ProductDescription: readString(source, 'ProductDescription', 'productDescription'),
    CustomerCode: readString(source, 'CustomerCode', 'customerCode'),
    CustomerName: readString(source, 'CustomerName', 'customerName', 'U_CustomerName', 'u_CustomerName'),
    Project: readString(source, 'Project', 'project'),
    ProjectName: readString(source, 'ProjectName', 'projectName'),
    Warehouse: readString(source, 'Warehouse', 'warehouse'),
    DrawingNo: readString(source, 'DrawingNo', 'drawingNo', 'U_DwgNo', 'u_DwgNo'),
    CreationDate: readString(source, 'CreationDate', 'creationDate'),
    PlannedQuantity: readNumber(source, 'PlannedQuantity', 'plannedQuantity'),
    Type: readString(source, 'Type', 'ProductionOrderType', 'productionOrderType'),
    ProductionCategory: readString(source, 'ProductionCategory', 'U_ProdType', 'u_ProdType'),
    ProductionOrderLines: productionOrderLines,
  }
}

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
  }
}
