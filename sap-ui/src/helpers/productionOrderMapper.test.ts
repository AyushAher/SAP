import { describe, expect, it } from 'vitest'
import {
  normalizeProductionOrder,
  toProductionOrderPayload,
  toProductionOrderSelectionPayload,
} from '@/helpers/productionOrderMapper'

/** A production order the way the API returns it: SAP names throughout. */
const sapOrder = {
  AbsoluteEntry: 646,
  DocumentNumber: 10,
  ItemNo: 'FG-001',
  ProductionOrderStatus: 'boposPlanned',
  ProductionOrderType: 'bopotSpecial',
  U_ProdType: 'INT',
  U_DwgNo: 'DWG-42',
  U_PrjName: 'Refinery upgrade',
  U_CustomerName: 'Acme Industries',
  ProductDescription: 'Finished pump',
  CustomerCode: 'C000017',
  Project: 'PB/R&M/25262053',
  Warehouse: 'WIP',
  PlannedQuantity: 12,
  CompletedQuantity: 3,
  RejectedQuantity: 1,
  Priority: 100,
  PostingDate: '2026-06-16T00:00:00Z',
  DueDate: '2026-06-27T00:00:00Z',
  StartDate: '2026-06-18T00:00:00Z',
  ProductionOrderOriginNumber: 252610128,
  ProductionOrderOriginEntry: 156,
  Remarks: 'Rush job',
  ProductionOrderLines: [
    {
      LineNumber: 0,
      ItemNo: 'RM-100',
      ItemName: 'Steel plate',
      PlannedQuantity: 24,
      IssuedQuantity: 4,
      Warehouse: 'Store1',
      UoMCode: 6,
      U_FreeTxt: 'cut to size',
    },
  ],
}

describe('normalizeProductionOrder', () => {
  it('exposes every header field under exactly one name', () => {
    const view = normalizeProductionOrder(sapOrder)

    expect(view.ItemNumber).toBe('FG-001')
    expect(view.Status).toBe('boposPlanned')
    expect(view.Type).toBe('bopotSpecial')
    expect(view.ProductionCategory).toBe('INT')
    expect(view.DrawingNo).toBe('DWG-42')
    expect(view.ProjectName).toBe('Refinery upgrade')
    expect(view.CustomerName).toBe('Acme Industries')
    expect(view.SalesOrderDocNum).toBe(252610128)
    expect(view.SalesOrderDocEntry).toBe(156)

    // The raw SAP names must not survive alongside the friendly ones: two spellings of the same
    // field let a dropdown update one while the outgoing body was built from the other.
    for (const shadow of [
      'ItemNo',
      'ProductionOrderStatus',
      'ProductionOrderType',
      'U_ProdType',
      'U_DwgNo',
      'U_PrjName',
      'U_CustomerName',
      'ProductionOrderOriginNumber',
      'ProductionOrderOriginEntry',
    ]) {
      expect(Object.keys(view)).not.toContain(shadow)
    }
  })

  it('reads a camelCase response as well', () => {
    const view = normalizeProductionOrder({
      absoluteEntry: 7,
      itemNo: 'FG-009',
      productionOrderStatus: 'boposReleased',
      u_ProdType: 'JOB',
      plannedQuantity: 4,
    })

    expect(view.AbsoluteEntry).toBe(7)
    expect(view.ItemNumber).toBe('FG-009')
    expect(view.Status).toBe('boposReleased')
    expect(view.ProductionCategory).toBe('JOB')
    expect(view.PlannedQuantity).toBe(4)
  })
})

describe('toProductionOrderPayload', () => {
  it('sends the SAP names the request model binds when creating', () => {
    const payload = toProductionOrderPayload({
      ItemNumber: 'FG-001',
      Status: 'boposPlanned',
      Type: 'bopotDisassembly',
      ProductionCategory: 'JOB',
      DrawingNo: 'DWG-7',
      CustomerCode: 'C000017',
      Project: 'PRJ-1',
      Warehouse: 'Subcon',
      IssWarehouse: 'Store1',
      PlannedQuantity: 5,
      SalesOrderDocNum: 252610128,
      SalesOrderDocEntry: 156,
      PostingDate: '2026-08-13',
      DueDate: '2026-08-20',
      StartDate: '2026-08-13',
      Remarks: 'Rush',
      ProductionOrderLines: [{ ItemNo: 'RM-100', PlannedQuantity: 10, Warehouse: 'Store1', LineNumber: 1 }],
    })

    const json = JSON.parse(JSON.stringify(payload))
    expect(json.ItemNo).toBe('FG-001')
    expect(json.ProductionOrderStatus).toBe('boposPlanned')
    expect(json.ProductionOrderType).toBe('bopotDisassembly')
    expect(json.U_ProdType).toBe('JOB')
    expect(json.U_DwgNo).toBe('DWG-7')
    expect(json.ProductionOrderOriginNumber).toBe(252610128)
    expect(json.ProductionOrderOriginEntry).toBe(156)
    expect(json.Warehouse).toBe('Subcon')
    expect(json.PlannedQuantity).toBe(5)
    expect(json.DueDate).toBe('2026-08-20')
    expect(json.StartDate).toBe('2026-08-13')
    expect(json.ProductionOrderLines).toHaveLength(1)

    // Friendly names bind to nothing on the API side, so they must not be what we send.
    for (const friendly of ['ItemNumber', 'Status', 'Type', 'ProductionCategory', 'DrawingNo', 'SalesOrderDocNum', 'SalesOrderDocEntry']) {
      expect(json).not.toHaveProperty(friendly)
    }
    // IssWarehouse is a UI-only seed for the line warehouses.
    expect(json).not.toHaveProperty('IssWarehouse')
  })

  it('always sends the fields the request model cannot leave out', () => {
    const payload = toProductionOrderPayload({ ItemNumber: 'FG-001' })

    expect(payload.PlannedQuantity).toBe(0)
    expect(payload.CompletedQuantity).toBe(0)
    expect(payload.RejectedQuantity).toBe(0)
    expect(payload.PostingDate).toBeTruthy()
  })

  it('echoes the quantities SAP already holds instead of zeroing them', () => {
    const payload = toProductionOrderPayload(normalizeProductionOrder(sapOrder))

    expect(payload.CompletedQuantity).toBe(3)
    expect(payload.RejectedQuantity).toBe(1)
    expect(payload.PostingDate).toBe('2026-06-16')
    expect(payload.DueDate).toBe('2026-06-27')
  })

  it('omits the optional user fields when empty so an update cannot blank them', () => {
    const payload = toProductionOrderPayload({
      ItemNumber: 'FG-001',
      ProductionCategory: '',
      DrawingNo: '',
      Remarks: '',
    })

    expect(payload).not.toHaveProperty('U_ProdType')
    expect(payload).not.toHaveProperty('U_DwgNo')
    expect(payload).not.toHaveProperty('Remarks')
  })

  it('sends what the user edited on a loaded order, not the value it was loaded with', () => {
    const view = normalizeProductionOrder(sapOrder)
    const edited = { ...view, Status: 'boposReleased', ProductionCategory: 'JOB', DrawingNo: 'DWG-99' }

    const payload = toProductionOrderPayload(edited)

    expect(payload.ProductionOrderStatus).toBe('boposReleased')
    expect(payload.U_ProdType).toBe('JOB')
    expect(payload.U_DwgNo).toBe('DWG-99')
  })

  it('takes explicit lines over the ones on the order and keeps line user fields', () => {
    const view = normalizeProductionOrder(sapOrder)
    const payload = toProductionOrderPayload(view, [
      { ...view.ProductionOrderLines![0], PlannedQuantity: 30 },
    ])

    const lines = payload.ProductionOrderLines as Array<Record<string, unknown>>
    expect(lines).toHaveLength(1)
    expect(lines[0].PlannedQuantity).toBe(30)
    expect(lines[0].ItemNo).toBe('RM-100')
    expect(lines[0].UoMCode).toBe(6)
    expect(lines[0].U_FreeTxt).toBe('cut to size')
  })
})

describe('toProductionOrderSelectionPayload', () => {
  it('translates the order of an issue or receipt draft back to SAP names', () => {
    const payload = toProductionOrderSelectionPayload({
      ProductionOrder: normalizeProductionOrder(sapOrder),
      ProductionOrderLinesEntryNumber: [{ ItemNo: 'RM-100', IssuedQuantity: 2, LineNumber: 0 }],
      WorkerName: 'R. Patil',
    })

    const order = payload.ProductionOrder as Record<string, unknown>
    expect(order.ItemNo).toBe('FG-001')
    expect(order.ProductionOrderStatus).toBe('boposPlanned')
    expect(order.U_CustomerName).toBe('Acme Industries')
    expect(order.U_PrjName).toBe('Refinery upgrade')
    expect(payload.WorkerName).toBe('R. Patil')
    expect(payload.ProductionOrderLinesEntryNumber).toHaveLength(1)
  })
})
