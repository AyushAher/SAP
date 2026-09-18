import { PRODUCTION_ORDER_TYPE_SPECIAL, type ProductionOrder, type ProductionOrderLine, type SalesOrderProductLine } from '@/types/production'

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
 * Inventory quantity the sales order line implies: InventoryQuantity when SAP sent it, otherwise
 * sales Quantity × items-per-unit (UnitsOfMeasurment).
 */
export function salesOrderLineInventoryQty(line: SalesOrderProductLine): number {
  if (line.InventoryQuantity != null && Number.isFinite(line.InventoryQuantity) && line.InventoryQuantity > 0) {
    return line.InventoryQuantity
  }
  const qty = line.Quantity ?? 0
  const itemsPerUnit = line.UnitsOfMeasurment != null && line.UnitsOfMeasurment > 0 ? line.UnitsOfMeasurment : 1
  return qty * itemsPerUnit
}

/** Sum of inventory qty on the sales order for this finished-good item. */
export function salesOrderPlannedQtyCap(
  products: SalesOrderProductLine[] | null | undefined,
  itemCode?: string | null,
): number | undefined {
  const code = (itemCode ?? '').trim().toUpperCase()
  if (!products || !code) return undefined
  let cap = 0
  let matched = false
  for (const line of products) {
    if ((line.ItemCode ?? '').trim().toUpperCase() !== code) continue
    matched = true
    cap += salesOrderLineInventoryQty(line)
  }
  return matched ? cap : undefined
}

export function isItemOnSalesOrder(
  products: SalesOrderProductLine[] | null | undefined,
  itemCode?: string | null,
): boolean {
  const code = (itemCode ?? '').trim().toUpperCase()
  if (!products || !code) return false
  return products.some((line) => (line.ItemCode ?? '').trim().toUpperCase() === code)
}

export function filterSalesOrderProducts(
  products: SalesOrderProductLine[],
  search: string,
): SalesOrderProductLine[] {
  const term = search.trim().toLowerCase()
  const seen = new Set<string>()
  const matches: SalesOrderProductLine[] = []
  for (const line of products) {
    const code = (line.ItemCode ?? '').trim()
    if (!code || seen.has(code.toUpperCase())) continue
    const name = (line.ItemName ?? '').trim()
    if (term && !code.toLowerCase().includes(term) && !name.toLowerCase().includes(term)) continue
    seen.add(code.toUpperCase())
    matches.push(line)
  }
  return matches
}

/**
 * Save rules for the production order form. The first four are what the legacy form required;
 * the quantity and component-line rules are new. An issued-versus-planned check deliberately
 * lives in the issue flow instead, where issued quantities are entered.
 * When sales-order lines are supplied, Product No. must be on that order and Planned Qty must
 * not exceed the sales-order inventory quantity (quantity × items per unit).
 */
export function validateProductionOrderForm(
  order: ProductionOrder,
  lines: ProductionOrderLine[],
  salesOrderProducts?: SalesOrderProductLine[] | null,
  options?: { requireSubassemblyWithItems?: boolean; subassemblies?: ProductionOrder[] },
): string | null {
  void lines
  if (!order.ItemNumber) return 'Product No. is required.'
  // SAP-native orders are often origin Manual with no sales order. Require it only when creating.
  if (!order.SalesOrderDocNum && order.AbsoluteEntry == null) return 'Sales Order is required.'
  if (!order.Warehouse) return 'Receipt warehouse could not be set from the production category.'
  if (!order.PlannedQuantity || order.PlannedQuantity <= 0) return 'Planned quantity must be greater than zero.'
  if (salesOrderProducts && salesOrderProducts.length > 0) {
    if (!isItemOnSalesOrder(salesOrderProducts, order.ItemNumber)) {
      return 'Product No. must be an item on the selected sales order.'
    }
    const cap = salesOrderPlannedQtyCap(salesOrderProducts, order.ItemNumber)
    if (cap != null && order.PlannedQuantity > cap) {
      return `Planned quantity cannot exceed ${cap} (sales order quantity × items per unit).`
    }
  }
  if (options?.requireSubassemblyWithItems) {
    const ready = (options.subassemblies ?? []).filter(
      (row) => (row.ProductionOrderLines ?? []).some((line) => line.ItemNo && (line.PlannedQuantity ?? 0) > 0),
    )
    if (ready.length < 1) return 'Add at least one sub-assembly with items.'
  }
  return null
}

export function validateSubassemblyHeaderForm(order: ProductionOrder): string | null {
  if (!order.ItemNumber) return 'Product No. is required.'
  if (!order.PlannedQuantity || order.PlannedQuantity <= 0) return 'Planned quantity must be greater than zero.'
  if (!order.ParentProductionOrderNo) return 'Parent production order is required.'
  return null
}

export function validateSubassemblyForm(order: ProductionOrder, lines: ProductionOrderLine[]): string | null {
  return validateSubassemblyHeaderForm(order) ?? validateSubassemblyItemsForm(lines)
}

/** Parent DocumentNumber used while a new production order is still a local draft. */
export const DRAFT_PRODUCTION_ORDER_NO = '0'

/** Parent DocNum from a stored sub-assembly no (`13/2` or `13-2` → `13`, legacy `13` → `13`). */
export function parentDocNumFromSubassemblyNo(value?: string | null): string {
  const raw = (value ?? '').trim()
  if (!raw) return ''
  const sep = Math.max(raw.lastIndexOf('/'), raw.lastIndexOf('-'))
  if (sep <= 0) return raw
  const parent = raw.slice(0, sep)
  const sequence = raw.slice(sep + 1)
  return /^\d+$/.test(parent) && /^\d+$/.test(sequence) ? parent : raw
}

/** Sequence from `{parent}/{seq}` or `{parent}-{seq}`. */
export function subassemblySequence(value?: string | null): number {
  const raw = (value ?? '').trim()
  const sep = Math.max(raw.lastIndexOf('/'), raw.lastIndexOf('-'))
  if (sep <= 0) return 0
  const parent = raw.slice(0, sep)
  const sequence = Number.parseInt(raw.slice(sep + 1), 10)
  if (!/^\d+$/.test(parent) || !Number.isFinite(sequence) || sequence <= 0) return 0
  return sequence
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
  let max = 0
  for (const row of existing) {
    const n = subassemblySequence(row.ParentProductionOrderNo)
    if (n > max) max = n
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
  const seq = subassemblySequence(udf)
  const parent = parentNo != null && String(parentNo) !== ''
    ? String(parentNo)
    : parentDocNumFromSubassemblyNo(udf)
  if (seq > 0) {
    if (!parent || parent === DRAFT_PRODUCTION_ORDER_NO) return `Pending/${seq}`
    return `${parent}/${seq}`
  }

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

/** Sub-assembly header/row status. Complete is SAP Closed (`boposClosed`). */
export const SUBASSEMBLY_STATUS_OPTIONS = [
  { value: 'boposPlanned', label: 'Planned' },
  { value: 'boposReleased', label: 'Released' },
  { value: 'boposClosed', label: 'Complete' },
]

export function subassemblyStatusLabel(status?: string): string {
  return SUBASSEMBLY_STATUS_OPTIONS.find((option) => option.value === status)?.label
    ?? productionOrderStatusLabel(status)
}

/** Drawing name stored on the virtual child header, or on component U_DwgName — never Free Text. */
export function subassemblyDrawingName(
  child?: ProductionOrder | null,
  parent?: ProductionOrder | null,
): string {
  const childDesc = (child?.ProductDescription ?? '').trim()
  const parentDesc = (parent?.ProductDescription ?? '').trim()
  if (childDesc && childDesc !== parentDesc) return childDesc
  const fromLines = (child?.ProductionOrderLines ?? [])
    .map((line) => (line.DrawingName ?? '').trim())
    .find(Boolean)
  return fromLines ?? ''
}

/**
 * Component (issue) warehouse for a sub-assembly line. The child header warehouse is the
 * receipt warehouse (WIP / Subcon); components must issue from Store1 (or the parent's
 * issuing warehouse). Sending the receipt warehouse on a new component is what SAP rejected
 * with Error -1 when updating sub-assembly 24/1.
 */
export function subassemblyComponentWarehouse(
  child?: ProductionOrder | null,
  parent?: ProductionOrder | null,
  lines: ProductionOrderLine[] = [],
): string {
  const receipt = child?.Warehouse || parent?.Warehouse
  const fromIss = child?.IssWarehouse || parent?.IssWarehouse
  if (fromIss) return fromIss
  const fromLine =
    lines.find((line) => line.Warehouse && line.Warehouse !== receipt)?.Warehouse
    || lines.find((line) => line.Warehouse)?.Warehouse
  return fromLine || 'Store1'
}

/** New component row for the sub-assembly items screen. LineNumber is omitted so SAP appends. */
export function buildSubassemblyItemLine(
  draft: ProductionOrderLine,
  child: ProductionOrder | null,
  parent: ProductionOrder | null,
  lines: ProductionOrderLine[],
  extras?: { drawingName?: string; status?: string },
): ProductionOrderLine {
  const drawingName = (draft.DrawingName ?? extras?.drawingName ?? '').trim()
  const status = draft.Status || extras?.status || child?.Status || 'boposPlanned'
  return {
    ItemNo: draft.ItemNo,
    ItemName: draft.ItemName,
    PlannedQuantity: draft.PlannedQuantity,
    Warehouse: subassemblyComponentWarehouse(child, parent, lines),
    ProductionOrderIssueType: draft.ProductionOrderIssueType || 'im_Manual',
    DrawingNo: (draft.DrawingNo ?? child?.DrawingNo ?? '').trim() || undefined,
    DrawingName: drawingName || undefined,
    FreeText: (draft.FreeText ?? '').trim() || undefined,
    Status: status,
  }
}

export function issuedQuantityTotal(order: ProductionOrder): number {
  return (order.ProductionOrderLines ?? []).reduce((sum, line) => sum + (line.IssuedQuantity ?? 0), 0)
}

/** Seeds a child from the parent product. Items are added later and written onto the parent SAP order. */
export function buildSubassemblyDraftFromParent(
  parent: ProductionOrder,
  existing: ProductionOrder[] = [],
): ProductionOrder {
  const parentNo = parent.DocumentNumber != null ? String(parent.DocumentNumber) : ''
  return {
    ItemNumber: parent.ItemNumber ?? '',
    ProductDescription: parent.ProductDescription ?? '',
    DrawingNo: '',
    Weight: undefined,
    PlannedQuantity: parent.PlannedQuantity && parent.PlannedQuantity > 0 ? parent.PlannedQuantity : 1,
    Status: 'boposPlanned',
    Type: PRODUCTION_ORDER_TYPE_SPECIAL,
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
    ParentProductionOrderNo: nextSubassemblyNumber(parentNo || DRAFT_PRODUCTION_ORDER_NO, existing),
    ParentAbsoluteEntry: parent.AbsoluteEntry,
    ProductionOrderLines: [],
  }
}

/** WOR1 U_DocNum marks a component as belonging to a portal sub-assembly, not the parent BOM. */
export function isSubassemblyComponentLine(line: ProductionOrderLine): boolean {
  const raw = line as ProductionOrderLine & { U_DocNum?: string }
  const tag = (line.DocNum ?? raw.U_DocNum ?? '').toString().trim()
  if (!tag) return false
  const sep = Math.max(tag.lastIndexOf('/'), tag.lastIndexOf('-'))
  if (sep <= 0) return false
  const parent = tag.slice(0, sep)
  const sequence = tag.slice(sep + 1)
  return /^\d+$/.test(parent) && /^\d+$/.test(sequence)
}

