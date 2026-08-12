import { describe, expect, it } from 'vitest'
import {
  applyProductionCategoryDefaults,
  validateProductionOrderForm,
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
