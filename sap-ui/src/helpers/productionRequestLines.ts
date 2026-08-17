import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

/** Header FG is not a BOM line, so it has no production-order LineNumber. */
export function isFinishedGoodReceiptLine(order: ProductionOrder, line: ProductionOrderLine): boolean {
  if (!order.ItemNumber || line.ItemNo !== order.ItemNumber)
    return false
  const onBom = order.ProductionOrderLines?.some(
    (poLine) => poLine.ItemNo === line.ItemNo && poLine.LineNumber === line.LineNumber,
  )
  return !onBom
}

export function buildFinishedGoodReceiptLine(order: ProductionOrder): ProductionOrderLine {
  const planned = order.PlannedQuantity ?? 0
  const completed = order.CompletedQuantity ?? 0
  return {
    ItemNo: order.ItemNumber,
    ItemName: order.ProductDescription,
    PlannedQuantity: planned,
    IssuedQuantity: Math.max(0, planned - completed),
    Warehouse: order.Warehouse,
    DocumentAbsoluteEntry: order.AbsoluteEntry,
  }
}

export function ensureFinishedGoodReceiptLine(
  order: ProductionOrder,
  lines: ProductionOrderLine[],
): ProductionOrderLine[] {
  if (!order.ItemNumber) return lines
  if (lines.some((line) => isFinishedGoodReceiptLine(order, line))) return lines
  return [buildFinishedGoodReceiptLine(order), ...lines]
}

export function alreadyIssuedQuantity(order: ProductionOrder | undefined, line: ProductionOrderLine): number {
  if (!order) return 0
  if (isFinishedGoodReceiptLine(order, line)) return order.CompletedQuantity ?? 0
  return order.ProductionOrderLines?.find(
    (poLine) => poLine.LineNumber === line.LineNumber && poLine.ItemNo === line.ItemNo,
  )?.IssuedQuantity ?? 0
}
