import { describe, expect, it } from 'vitest'
import {
  applyProductionCategoryDefaults,
  buildSubassemblyDraftFromParent,
  formatSubassemblyNo,
  nextSubassemblyNumber,
  validateProductionOrderForm,
  validateSubassemblyHeaderForm,
  validateSubassemblyItemsForm,
} from '@/helpers/productionOrderForm'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

const validOrder: ProductionOrder = {
  ItemNumber: 'FG-001',
  SalesOrderDocNum: 252610128,
  Warehouse: 'Subcon',
  IssWarehouse: 'Store1',
  PlannedQuantity: 5,
}

const validLines: ProductionOrderLine[] = [
  { ItemNo: 'RM-100', PlannedQuantity: 10, Warehouse: 'Store1', LineNumber: 1 },
]

describe('validateProductionOrderForm', () => {
  it('accepts a complete order', () => {
    expect(validateProductionOrderForm(validOrder, validLines)).toBeNull()
  })

  it.each([
    ['ItemNumber', 'Product No. is required.'],
    ['SalesOrderDocNum', 'Sales Order is required.'],
    ['Warehouse', 'Receipt Warehouse is required.'],
    ['IssWarehouse', 'Issuing Warehouse is required.'],
  ] as const)('requires %s', (field, message) => {
    expect(validateProductionOrderForm({ ...validOrder, [field]: undefined }, validLines)).toBe(message)
  })

  it('requires a planned quantity greater than zero', () => {
    expect(validateProductionOrderForm({ ...validOrder, PlannedQuantity: 0 }, validLines))
      .toBe('Planned quantity must be greater than zero.')
  })

  it('requires at least one component line', () => {
    expect(validateProductionOrderForm(validOrder, []))
      .toBe('Add at least one component line.')
  })

  it('requires each component line to have an item and a quantity', () => {
    expect(validateProductionOrderForm(validOrder, [{ PlannedQuantity: 1 }]))
      .toBe('Every component line needs an item.')
    expect(validateProductionOrderForm(validOrder, [{ ItemNo: 'RM-100', PlannedQuantity: 0 }]))
      .toBe('Every component line needs a quantity greater than zero.')
  })
})

describe('applyProductionCategoryDefaults', () => {
  it.each([
    ['JOB', 'Subcon', 'Store1'],
    ['EXT', 'PBPL(S)', 'PBPL(S)'],
    ['INT', 'WIP', 'Store1'],
  ])('sets the warehouses %s implies', (category, warehouse, issWarehouse) => {
    const applied = applyProductionCategoryDefaults(category, validOrder, validLines)

    expect(applied.order.ProductionCategory).toBe(category)
    expect(applied.order.Warehouse).toBe(warehouse)
    expect(applied.order.IssWarehouse).toBe(issWarehouse)
    expect(applied.lines[0].Warehouse).toBe(issWarehouse)
  })

  it('leaves the warehouses alone for a category it does not know', () => {
    const applied = applyProductionCategoryDefaults('OTHER', validOrder, validLines)

    expect(applied.order.Warehouse).toBe('Subcon')
    expect(applied.lines[0].Warehouse).toBe('Store1')
  })

  it('does not mutate the order or lines it was given', () => {
    const order = { ...validOrder }
    const lines = [{ ...validLines[0] }]

    applyProductionCategoryDefaults('EXT', order, lines)

    expect(order.Warehouse).toBe('Subcon')
    expect(lines[0].Warehouse).toBe('Store1')
  })
})

describe('validateSubassemblyHeaderForm', () => {
  const validChild: ProductionOrder = {
    ItemNumber: 'SA-001',
    PlannedQuantity: 2,
    ParentProductionOrderNo: '10',
    Warehouse: 'WIP',
  }

  it('accepts a complete sub-assembly header', () => {
    expect(validateSubassemblyHeaderForm(validChild)).toBeNull()
  })

  it('requires the product, quantity, parent, and receipt warehouse', () => {
    expect(validateSubassemblyHeaderForm({ ...validChild, ItemNumber: undefined }))
      .toBe('Product No. is required.')
    expect(validateSubassemblyHeaderForm({ ...validChild, PlannedQuantity: 0 }))
      .toBe('Planned quantity must be greater than zero.')
    expect(validateSubassemblyHeaderForm({ ...validChild, ParentProductionOrderNo: undefined }))
      .toBe('Parent production order is required.')
    expect(validateSubassemblyHeaderForm({ ...validChild, Warehouse: undefined }))
      .toBe('Receipt Warehouse is required.')
  })
})

describe('validateSubassemblyItemsForm', () => {
  it('requires at least one item with a code and quantity', () => {
    expect(validateSubassemblyItemsForm([])).toBe('Add at least one item.')
    expect(validateSubassemblyItemsForm([{ PlannedQuantity: 1 }])).toBe('Every item needs an item code.')
    expect(validateSubassemblyItemsForm([{ ItemNo: 'RM-100', PlannedQuantity: 0 }]))
      .toBe('Every item needs a quantity greater than zero.')
    expect(validateSubassemblyItemsForm([{ ItemNo: 'RM-100', PlannedQuantity: 4 }])).toBeNull()
  })
})

describe('buildSubassemblyDraftFromParent', () => {
  it('copies the parent product and assigns the next parent/sequence number', () => {
    const draft = buildSubassemblyDraftFromParent({
      AbsoluteEntry: 646,
      DocumentNumber: 10,
      ItemNumber: 'FG-001',
      ProductDescription: 'FINISHED GOOD',
      CustomerCode: 'C000017',
      Project: 'PRJ-1',
      Warehouse: 'WIP',
      IssWarehouse: 'Store1',
      Type: 'bopotSpecial',
      ProductionCategory: 'INT',
      PlannedQuantity: 12,
      SalesOrderDocNum: 252610128,
      SalesOrderDocEntry: 156,
    })

    expect(draft.ParentProductionOrderNo).toBe('10/1')
    expect(draft.ItemNumber).toBe('FG-001')
    expect(draft.ProductDescription).toBe('')
    expect(draft.DrawingNo).toBe('')
    expect(draft.CustomerCode).toBe('C000017')
    expect(draft.Project).toBe('PRJ-1')
    expect(draft.Warehouse).toBe('WIP')
    expect(draft.Type).toBe('bopotSpecial')
    expect(draft.ProductionCategory).toBe('INT')
    expect(draft.SalesOrderDocNum).toBe(252610128)
    expect(draft.ProductionOrderLines).toEqual([])
  })

  it('increments past existing and legacy sibling numbers', () => {
    const draft = buildSubassemblyDraftFromParent(
      { DocumentNumber: 13, ItemNumber: 'FG-001', Warehouse: 'WIP', PlannedQuantity: 1 },
      [
        { ParentProductionOrderNo: '13' },
        { ParentProductionOrderNo: '13' },
        { ParentProductionOrderNo: '13/3' },
      ],
    )

    expect(draft.ParentProductionOrderNo).toBe('13/4')
  })
})

describe('nextSubassemblyNumber', () => {
  it('starts at /1 and skips cancelled numbers that are still stored', () => {
    expect(nextSubassemblyNumber(13, [])).toBe('13/1')
    expect(nextSubassemblyNumber(13, [{ ParentProductionOrderNo: '13/1' }])).toBe('13/2')
    expect(nextSubassemblyNumber(13, [
      { ParentProductionOrderNo: '13/1' },
      { ParentProductionOrderNo: '13/2' },
    ])).toBe('13/3')
  })
})

describe('formatSubassemblyNo', () => {
  it('prefers the stored parent/sequence value', () => {
    expect(formatSubassemblyNo({ ParentProductionOrderNo: '13/2' }, 13)).toBe('13/2')
  })

  it('derives a sequence for legacy rows that only stored the parent no', () => {
    const siblings = [
      { AbsoluteEntry: 1, ParentProductionOrderNo: '13' },
      { AbsoluteEntry: 2, ParentProductionOrderNo: '13' },
    ]
    expect(formatSubassemblyNo(siblings[1], 13, siblings)).toBe('13/2')
  })
})
