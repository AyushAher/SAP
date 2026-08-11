import { describe, expect, it } from 'vitest'
import {
  validateManualProductionLineQuantities,
  validateProductionRequestForSave,
} from '@/helpers/productionRequestValidation'
import type { ProductionOrderSelection } from '@/types/production'

function selection(overrides?: Partial<ProductionOrderSelection>): ProductionOrderSelection {
  return {
    ProductionOrder: {
      AbsoluteEntry: 1,
      DocumentNumber: 100,
      Project: 'P1',
      ProjectName: 'Project One',
    },
    ProductionOrderLinesEntryNumber: [
      { LineNumber: 0, ItemNo: 'RM-1', PlannedQuantity: 10, IssuedQuantity: 4, Warehouse: 'WH01' },
    ],
    ...overrides,
  }
}

describe('validateProductionRequestForSave', () => {
  it('requires a production order', () => {
    expect(validateProductionRequestForSave(null)).toBe('Please select a production order.')
    expect(
      validateProductionRequestForSave({
        ProductionOrder: undefined as never,
        ProductionOrderLinesEntryNumber: [],
      }),
    ).toBe('Please select a production order.')
  })

  it('allows saving with no lines so items can be added afterwards', () => {
    expect(
      validateProductionRequestForSave(selection({ ProductionOrderLinesEntryNumber: [] })),
    ).toBeNull()
  })

  it('rejects issued quantity above planned', () => {
    expect(
      validateProductionRequestForSave(
        selection({
          ProductionOrderLinesEntryNumber: [
            { LineNumber: 0, ItemNo: 'RM-1', PlannedQuantity: 2, IssuedQuantity: 5 },
          ],
        }),
      ),
    ).toBe('Issued quantity cannot exceed planned quantity for any line item.')
  })

  it('accepts valid issue and receipt drafts', () => {
    expect(validateProductionRequestForSave(selection())).toBeNull()
  })
})

describe('validateManualProductionLineQuantities', () => {
  it('rejects issued above planned', () => {
    expect(validateManualProductionLineQuantities(5, 2)).toBe(
      'Issue quantity cannot exceed planned quantity.',
    )
  })

  it('allows issued equal to planned', () => {
    expect(validateManualProductionLineQuantities(2, 2)).toBeNull()
  })
})
