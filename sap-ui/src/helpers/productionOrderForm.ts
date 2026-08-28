import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

/**
 * Receipt and issuing warehouses implied by the production category, mirroring the legacy form:
 * JOB is made at a sub-contractor, EXT on the customer site, INT in the factory.
 */
const CATEGORY_WAREHOUSES: Record<string, { warehouse: string; issWarehouse: string }> = {
  JOB: { warehouse: 'Subcon', issWarehouse: 'Store1' },
  EXT: { warehouse: 'PBPL(S)', issWarehouse: 'PBPL(S)' },
  INT: { warehouse: 'WIP', issWarehouse: 'Store1' },
}

/**
 * Applies the warehouses a production category implies. Called when the category changes rather
 * than at submit, so the user can see and override what it did before saving.
 */
export function applyProductionCategoryDefaults(
  category: string,
  order: ProductionOrder,
  lines: ProductionOrderLine[],
): { order: ProductionOrder; lines: ProductionOrderLine[] } {
  const defaults = CATEGORY_WAREHOUSES[category]
  const updated: ProductionOrder = { ...order, ProductionCategory: category }
  if (!defaults) return { order: updated, lines: lines.map((line) => ({ ...line })) }

  updated.Warehouse = defaults.warehouse
  updated.IssWarehouse = defaults.issWarehouse
  return {
    order: updated,
    lines: lines.map((line) => ({ ...line, Warehouse: defaults.issWarehouse })),
  }
}

/**
 * Save rules for the production order form. The first four are what the legacy form required;
 * the quantity and component-line rules are new. An issued-versus-planned check deliberately
 * lives in the issue flow instead, where issued quantities are entered.
 */
export function validateProductionOrderForm(
  order: ProductionOrder,
  lines: ProductionOrderLine[],
): string | null {
  if (!order.ItemNumber) return 'Product No. is required.'
  if (!order.SalesOrderDocNum) return 'Sales Order is required.'
  if (!order.Warehouse) return 'Receipt Warehouse is required.'
  if (!order.IssWarehouse) return 'Issuing Warehouse is required.'
  if (!order.PlannedQuantity || order.PlannedQuantity <= 0) return 'Planned quantity must be greater than zero.'
  if (!lines.length) return 'Add at least one component line.'
  if (lines.some((line) => !line.ItemNo)) return 'Every component line needs an item.'
  if (lines.some((line) => (line.PlannedQuantity ?? 0) <= 0)) return 'Every component line needs a quantity greater than zero.'
  return null
}

export function validateSubassemblyHeaderForm(order: ProductionOrder): string | null {
  if (!order.ItemNumber) return 'Product No. is required.'
  if (!order.PlannedQuantity || order.PlannedQuantity <= 0) return 'Planned quantity must be greater than zero.'
  if (!order.ParentProductionOrderNo) return 'Parent production order is required.'
  if (!order.Warehouse) return 'Receipt Warehouse is required.'
  const lines = order.ProductionOrderLines ?? []
  if (!lines.some((line) => line.ItemNo)) {
    return 'Parent production order has no component lines to copy onto the sub-assembly.'
  }
  return null
}

/** Parent DocNum from a stored sub-assembly no (`13/2` → `13`, legacy `13` → `13`). */
export function parentDocNumFromSubassemblyNo(value?: string | null): string {
  const raw = (value ?? '').trim()
  if (!raw) return ''
  const slash = raw.indexOf('/')
  return slash < 0 ? raw : raw.slice(0, slash)
}

/**
 * Next sub-assembly number: `{parentDocNum}/{i++}`. Sequence includes cancelled siblings so
 * numbers are not reused.
 */
export function nextSubassemblyNumber(
  parentNo: string | number,
  existing: ProductionOrder[],
): string {
  const parent = String(parentNo)
  const prefix = `${parent}/`
  let max = 0
  for (const row of existing) {
    const raw = (row.ParentProductionOrderNo ?? '').trim()
    if (raw.startsWith(prefix)) {
      const n = Number.parseInt(raw.slice(prefix.length), 10)
      if (Number.isFinite(n) && n > max) max = n
    }
  }
  const legacyCount = existing.filter((row) => (row.ParentProductionOrderNo ?? '').trim() === parent).length
  return `${parent}/${Math.max(max, legacyCount) + 1}`
}

/** UI label for a child: stored `13/2`, or `{parent}/{index}` for legacy rows that only stored the parent no. */
export function formatSubassemblyNo(
  order: ProductionOrder,
  parentNo?: string | number | null,
  siblings: ProductionOrder[] = [],
): string {
  const udf = (order.ParentProductionOrderNo ?? '').trim()
  if (udf.includes('/')) return udf

  const parent = parentNo != null && String(parentNo) !== ''
    ? String(parentNo)
    : udf
  if (!parent) return order.DocumentNumber != null ? String(order.DocumentNumber) : ''
  if (siblings.length === 0) return parent

  const sorted = [...siblings].sort(
    (a, b) => (a.AbsoluteEntry ?? 0) - (b.AbsoluteEntry ?? 0),
  )
  const index = sorted.findIndex((row) => row.AbsoluteEntry === order.AbsoluteEntry)
  return index >= 0 ? `${parent}/${index + 1}` : parent
}

export function validateSubassemblyItemsForm(lines: ProductionOrderLine[]): string | null {
  if (!lines.length) return 'Add at least one item.'
  if (lines.some((line) => !line.ItemNo)) return 'Every item needs an item code.'
  if (lines.some((line) => (line.PlannedQuantity ?? 0) <= 0)) return 'Every item needs a quantity greater than zero.'
  return null
}

export function productionOrderStatusLabel(status?: string): string {
  switch (status) {
    case 'boposPlanned':
      return 'Planned'
    case 'boposReleased':
      return 'Released'
    case 'boposClosed':
      return 'Closed'
    case 'boposCancelled':
      return 'Cancelled'
    default:
      return status || '—'
  }
}

/** Seeds a child from the parent product and copies parent component lines so SAP will accept the create. */
export function buildSubassemblyDraftFromParent(
  parent: ProductionOrder,
  existing: ProductionOrder[] = [],
): ProductionOrder {
  const parentNo = parent.DocumentNumber != null ? String(parent.DocumentNumber) : ''
  const issuingWarehouse = parent.IssWarehouse || undefined
  return {
    ItemNumber: parent.ItemNumber ?? '',
    ProductDescription: parent.ProductDescription ?? '',
    DrawingNo: '',
    PlannedQuantity: parent.PlannedQuantity && parent.PlannedQuantity > 0 ? parent.PlannedQuantity : 1,
    Status: 'boposPlanned',
    Type: parent.Type ?? 'bopotStandard',
    ProductionCategory: parent.ProductionCategory ?? 'JOB',
    CustomerCode: parent.CustomerCode,
    CustomerName: parent.CustomerName,
    Project: parent.Project,
    ProjectName: parent.ProjectName,
    Warehouse: parent.Warehouse,
    IssWarehouse: parent.IssWarehouse,
    SalesOrderDocNum: parent.SalesOrderDocNum,
    SalesOrderDocEntry: parent.SalesOrderDocEntry,
    PostingDate: parent.PostingDate,
    StartDate: parent.StartDate,
    DueDate: parent.DueDate,
    Remarks: parent.Remarks,
    ParentProductionOrderNo: parentNo ? nextSubassemblyNumber(parentNo, existing) : '',
    ProductionOrderLines: (parent.ProductionOrderLines ?? [])
      .filter((line) => line.ItemNo)
      .map((line) => ({
        ItemNo: line.ItemNo,
        ItemName: line.ItemName,
        PlannedQuantity: line.PlannedQuantity,
        Warehouse: issuingWarehouse || line.Warehouse,
        ProductionOrderIssueType: line.ProductionOrderIssueType,
      })),
  }
}
