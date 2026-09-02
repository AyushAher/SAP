import { describe, expect, it } from 'vitest'
import {
  applyProductionCategoryDefaults,
  buildSubassemblyDraftFromParent,
  buildSubassemblyItemLine,
  filterSalesOrderProducts,
  formatSubassemblyNo,
  isSubassemblyComponentLine,
  nextSubassemblyNumber,
  salesOrderPlannedQtyCap,
  subassemblyComponentWarehouse,
  validateProductionOrderForm,
  validateSubassemblyForm,
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
    ['Warehouse', 'Receipt warehouse could not be set from the production category.'],
  ] as const)('requires %s', (field, message) => {
    expect(validateProductionOrderForm({ ...validOrder, [field]: undefined }, validLines)).toBe(message)
  })

  it('requires a planned quantity greater than zero', () => {
    expect(validateProductionOrderForm({ ...validOrder, PlannedQuantity: 0 }, validLines))
      .toBe('Planned quantity must be greater than zero.')
  })

  it('does not require stand-alone component lines', () => {
    expect(validateProductionOrderForm(validOrder, [])).toBeNull()
  })

  it('requires at least one sub-assembly with items when creating', () => {
    expect(validateProductionOrderForm(validOrder, [], null, {
      requireSubassemblyWithItems: true,
      subassemblies: [{ ParentProductionOrderNo: '10/1', ProductionOrderLines: [] }],
    })).toBe('Add at least one sub-assembly with items.')
    expect(validateProductionOrderForm(validOrder, [], null, {
      requireSubassemblyWithItems: true,
      subassemblies: [{
        ParentProductionOrderNo: '10/1',
        ProductionOrderLines: [{ ItemNo: 'RM-1', PlannedQuantity: 2 }],
      }],
    })).toBeNull()
  })

  it('allows an existing SAP order with no sales order and no sub-assemblies', () => {
    expect(validateProductionOrderForm({
      ...validOrder,
      AbsoluteEntry: 641,
      SalesOrderDocNum: undefined,
    }, [])).toBeNull()
  })

  it('requires the product to be an item on the linked sales order', () => {
    expect(validateProductionOrderForm(validOrder, validLines, [
      { ItemCode: 'OTHER', Quantity: 5, UnitsOfMeasurment: 1 },
    ])).toBe('Product No. must be an item on the selected sales order.')
  })

  it('rejects a planned quantity above the sales order quantity × items per unit', () => {
    expect(validateProductionOrderForm({ ...validOrder, PlannedQuantity: 11 }, validLines, [
      { ItemCode: 'FG-001', Quantity: 2, UnitsOfMeasurment: 5 },
    ])).toBe('Planned quantity cannot exceed 10 (sales order quantity × items per unit).')
    expect(validateProductionOrderForm({ ...validOrder, PlannedQuantity: 10 }, validLines, [
      { ItemCode: 'FG-001', Quantity: 2, UnitsOfMeasurment: 5 },
    ])).toBeNull()
  })
})

describe('salesOrderPlannedQtyCap', () => {
  it('uses inventory quantity when SAP sent it', () => {
    expect(salesOrderPlannedQtyCap([
      { ItemCode: 'FG-001', Quantity: 2, UnitsOfMeasurment: 5, InventoryQuantity: 12 },
    ], 'FG-001')).toBe(12)
  })

  it('sums matching sales order lines', () => {
    expect(salesOrderPlannedQtyCap([
      { ItemCode: 'FG-001', Quantity: 1, UnitsOfMeasurment: 4 },
      { ItemCode: 'FG-001', Quantity: 1, UnitsOfMeasurment: 4 },
    ], 'fg-001')).toBe(8)
  })
})

describe('filterSalesOrderProducts', () => {
  it('returns unique items that match the search', () => {
    const rows = filterSalesOrderProducts([
      { ItemCode: 'FG-001', ItemName: 'Pump' },
      { ItemCode: 'FG-001', ItemName: 'Pump' },
      { ItemCode: 'FG-009', ItemName: 'Valve' },
    ], 'pump')
    expect(rows).toEqual([{ ItemCode: 'FG-001', ItemName: 'Pump' }])
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
    ProductionOrderLines: [{ ItemNo: 'RM-100', PlannedQuantity: 1 }],
  }

  it('accepts a complete sub-assembly header', () => {
    expect(validateSubassemblyHeaderForm(validChild)).toBeNull()
  })

  it('requires the product, quantity, and parent', () => {
    expect(validateSubassemblyHeaderForm({ ...validChild, ItemNumber: undefined }))
      .toBe('Product No. is required.')
    expect(validateSubassemblyHeaderForm({ ...validChild, PlannedQuantity: 0 }))
      .toBe('Planned quantity must be greater than zero.')
    expect(validateSubassemblyHeaderForm({ ...validChild, ParentProductionOrderNo: undefined }))
      .toBe('Parent production order is required.')
    expect(validateSubassemblyHeaderForm({ ...validChild, Warehouse: undefined })).toBeNull()
  })
})

describe('validateSubassemblyForm', () => {
  const validChild: ProductionOrder = {
    ItemNumber: 'SA-001',
    PlannedQuantity: 2,
    ParentProductionOrderNo: '10',
  }

  it('requires header fields and at least one item', () => {
    expect(validateSubassemblyForm(validChild, [])).toBe('Add at least one item.')
    expect(validateSubassemblyForm(validChild, [{ ItemNo: 'RM-100', PlannedQuantity: 4 }])).toBeNull()
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
  it('copies the parent product and assigns the next parent/sequence number without parent BOM lines', () => {
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
      ProductionOrderLines: [
        { LineNumber: 0, ItemNo: 'RM-100', ItemName: 'Steel', PlannedQuantity: 4, Warehouse: 'WIP' },
      ],
    })

    expect(draft.ParentProductionOrderNo).toBe('10/1')
    expect(draft.ItemNumber).toBe('FG-001')
    expect(draft.ProductDescription).toBe('FINISHED GOOD')
    expect(draft.DrawingNo).toBe('')
    expect(draft.CustomerCode).toBe('C000017')
    expect(draft.Project).toBe('PRJ-1')
    expect(draft.Warehouse).toBe('WIP')
    expect(draft.Type).toBe('bopotSpecial')
    expect(draft.ProductionCategory).toBe('INT')
    expect(draft.SalesOrderDocNum).toBe(252610128)
    expect(draft.ParentAbsoluteEntry).toBe(646)
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
    expect(draft.Type).toBe('bopotSpecial')
  })

  it('uses a pending parent number while the production order is still a local draft', () => {
    const draft = buildSubassemblyDraftFromParent(
      { ItemNumber: 'FG-001', Warehouse: 'Subcon', IssWarehouse: 'Store1', PlannedQuantity: 1 },
    )
    expect(draft.ParentProductionOrderNo).toBe('0/1')
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
    expect(nextSubassemblyNumber(13, [{ ParentProductionOrderNo: '0/1' }])).toBe('13/2')
  })
})

describe('subassemblyComponentWarehouse', () => {
  it('prefers the issuing warehouse over the receipt warehouse on the child header', () => {
    expect(subassemblyComponentWarehouse(
      { Warehouse: 'WIP', IssWarehouse: 'Store1' },
      { Warehouse: 'WIP' },
      [],
    )).toBe('Store1')
  })

  it('uses an existing component warehouse when IssWarehouse was not returned', () => {
    expect(subassemblyComponentWarehouse(
      { Warehouse: 'WIP' },
      { Warehouse: 'WIP' },
      [{ ItemNo: 'RM-1', Warehouse: 'Store1' }],
    )).toBe('Store1')
  })
})

describe('buildSubassemblyItemLine', () => {
  it('does not stamp LineNumber or the receipt warehouse on a newly added component', () => {
    expect(buildSubassemblyItemLine(
      { ItemNo: 'RM-2', ItemName: 'Channel', PlannedQuantity: 12 },
      { Warehouse: 'WIP', AbsoluteEntry: 661 },
      { Warehouse: 'WIP', IssWarehouse: 'Store1' },
      [{ LineNumber: 0, ItemNo: 'RM-1', Warehouse: 'Store1' }],
    )).toEqual({
      ItemNo: 'RM-2',
      ItemName: 'Channel',
      PlannedQuantity: 12,
      Warehouse: 'Store1',
      ProductionOrderIssueType: 'im_Manual',
      DrawingNo: undefined,
      FreeText: undefined,
    })
  })

  it('copies drawing no and free text onto the new component', () => {
    expect(buildSubassemblyItemLine(
      { ItemNo: 'RM-2', PlannedQuantity: 1, FreeText: 'cut extra' },
      { DrawingNo: 'DWG-9', ProductDescription: 'Spool A', Warehouse: 'WIP' },
      { IssWarehouse: 'Store1' },
      [],
    )).toEqual(expect.objectContaining({
      DrawingNo: 'DWG-9',
      FreeText: 'cut extra',
      Warehouse: 'Store1',
    }))
  })
})

describe('formatSubassemblyNo', () => {
  it('prefers the stored parent/sequence value', () => {
    expect(formatSubassemblyNo({ ParentProductionOrderNo: '13/2' }, 13)).toBe('13/2')
    expect(formatSubassemblyNo({ ParentProductionOrderNo: '0/1' }, 35)).toBe('35/1')
    expect(formatSubassemblyNo({ ParentProductionOrderNo: '0/1' })).toBe('Pending/1')
  })

  it('derives a sequence for legacy rows that only stored the parent no', () => {
    const siblings = [
      { AbsoluteEntry: 1, ParentProductionOrderNo: '13' },
      { AbsoluteEntry: 2, ParentProductionOrderNo: '13' },
    ]
    expect(formatSubassemblyNo(siblings[1], 13, siblings)).toBe('13/2')
  })
})

describe('isSubassemblyComponentLine', () => {
  it('treats a WOR1 U_DocNum as a portal sub-assembly tag', () => {
    expect(isSubassemblyComponentLine({ ItemNo: 'RM-100', DocNum: '10/1' })).toBe(true)
    expect(isSubassemblyComponentLine({ ItemNo: 'RM-100', U_DocNum: '10/1' } as ProductionOrderLine)).toBe(true)
    expect(isSubassemblyComponentLine({ ItemNo: 'RM-100', DocNum: '10-1' })).toBe(true)
    expect(isSubassemblyComponentLine({ ItemNo: 'RM-100', DocNum: 'test' })).toBe(false)
    expect(isSubassemblyComponentLine({ ItemNo: 'RM-100' })).toBe(false)
  })
})
