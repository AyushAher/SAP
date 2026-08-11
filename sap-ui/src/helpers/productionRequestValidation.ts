import type { ProductionOrderSelection } from '@/types/production'

/** Shared Issue/Receipt draft save rules (mirrors SapApi ProductionRequestMapper.ValidateForSave). */
export function validateProductionRequestForSave(
  selection: ProductionOrderSelection | null | undefined,
): string | null {
  if (!selection?.ProductionOrder) {
    return 'Please select a production order.'
  }

  const lines = selection.ProductionOrderLinesEntryNumber ?? []
  if (lines.some((line) => (line.IssuedQuantity ?? 0) > (line.PlannedQuantity ?? 0))) {
    return 'Issued quantity cannot exceed planned quantity for any line item.'
  }

  return null
}

export function validateManualProductionLineQuantities(
  issuedQuantity: number | undefined,
  plannedQuantity: number | undefined,
): string | null {
  if ((issuedQuantity ?? 0) > (plannedQuantity ?? 0)) {
    return 'Issue quantity cannot exceed planned quantity.'
  }
  return null
}
