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
