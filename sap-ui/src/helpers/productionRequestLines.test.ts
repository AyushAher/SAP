import { describe, expect, it } from 'vitest'
import {
  alreadyIssuedQuantity,
  buildFinishedGoodReceiptLine,
  ensureFinishedGoodReceiptLine,
  isFinishedGoodReceiptLine,
} from '@/helpers/productionRequestLines'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

const order: ProductionOrder = {
  AbsoluteEntry: 10,
  ItemNumber: 'FG-001',
  ProductDescription: 'Finished pump',
  Warehouse: 'FGWH',
  PlannedQuantity: 8,
  CompletedQuantity: 3,
  ProductionOrderLines: [
    { LineNumber: 0, ItemNo: 'RM-1', PlannedQuantity: 16, IssuedQuantity: 5, Warehouse: 'RMWH' },
  ],
}

describe('finished good receipt line', () => {
  it('builds remaining qty against the header item and receipt warehouse', () => {
    expect(buildFinishedGoodReceiptLine(order)).toEqual({
      ItemNo: 'FG-001',
      ItemName: 'Finished pump',
      PlannedQuantity: 8,
      IssuedQuantity: 5,
      Warehouse: 'FGWH',
      DocumentAbsoluteEntry: 10,
    })
  })

  it('inserts the FG line once when it is missing', () => {
    const components: ProductionOrderLine[] = [
      { LineNumber: 0, ItemNo: 'RM-1', PlannedQuantity: 16, IssuedQuantity: 2 },
    ]
    const withFg = ensureFinishedGoodReceiptLine(order, components)
    expect(withFg).toHaveLength(2)
    expect(isFinishedGoodReceiptLine(order, withFg[0])).toBe(true)
    expect(ensureFinishedGoodReceiptLine(order, withFg)).toBe(withFg)
  })

  it('offers the FG item in the picker list even when the BOM is empty', () => {
    const lines = ensureFinishedGoodReceiptLine(order, [])
    expect(lines).toHaveLength(1)
    expect(lines[0].ItemNo).toBe('FG-001')
    expect(lines[0].Warehouse).toBe('FGWH')
  })

  it('reads issued qty from completed qty for FG and from the PO line for components', () => {
    const fg = buildFinishedGoodReceiptLine(order)
    expect(alreadyIssuedQuantity(order, fg)).toBe(3)
    expect(alreadyIssuedQuantity(order, { LineNumber: 0, ItemNo: 'RM-1', IssuedQuantity: 99 })).toBe(5)
  })
})
