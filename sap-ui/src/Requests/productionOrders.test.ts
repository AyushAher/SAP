import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as apiClient from '@/helpers/api/client'
import { createProductionOrder, getProductionOrder, updateProductionOrder } from './productionOrders'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  invalidateCachedGets: vi.fn(),
}))

const apiGet = vi.mocked(apiClient.apiGet)
const apiPost = vi.mocked(apiClient.apiPost)
const apiPut = vi.mocked(apiClient.apiPut)

function bodyOf(mock: typeof apiPost | typeof apiPut) {
  return JSON.parse(JSON.stringify(mock.mock.calls[0][1])) as Record<string, unknown>
}

describe('createProductionOrder', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiPost.mockResolvedValue({ AbsoluteEntry: 647, DocumentNumber: 11 })
  })

  it('posts a body SAP names, including the manufactured product', async () => {
    await createProductionOrder({
      ItemNumber: 'FG-001',
      Status: 'boposPlanned',
      Type: 'bopotStandard',
      ProductionCategory: 'JOB',
      DrawingNo: 'DWG-7',
      PlannedQuantity: 5,
      Warehouse: 'Subcon',
      Project: 'PRJ-1',
      CustomerCode: 'C000017',
      SalesOrderDocNum: 252610128,
      SalesOrderDocEntry: 156,
      PostingDate: '2026-08-13',
      StartDate: '2026-08-13',
      DueDate: '2026-08-20',
      ProductionOrderLines: [{ ItemNo: 'RM-100', PlannedQuantity: 10, Warehouse: 'Store1', LineNumber: 1 }],
    })

    expect(apiPost).toHaveBeenCalledTimes(1)
    expect(apiPost.mock.calls[0][0]).toBe('/production-orders')

    const body = bodyOf(apiPost)
    expect(body.ItemNo).toBe('FG-001')
    expect(body.ProductionOrderStatus).toBe('boposPlanned')
    expect(body.ProductionOrderType).toBe('bopotStandard')
    expect(body.U_ProdType).toBe('JOB')
    expect(body.U_DwgNo).toBe('DWG-7')
    expect(body.ProductionOrderOriginNumber).toBe(252610128)
    expect(body.ProductionOrderOriginEntry).toBe(156)
    expect(body.DueDate).toBe('2026-08-20')
    expect(body.StartDate).toBe('2026-08-13')
    expect(body).not.toHaveProperty('ItemNumber')
    expect(body).not.toHaveProperty('Status')
  })
})

describe('updateProductionOrder', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiPut.mockResolvedValue({ AbsoluteEntry: 646, DocumentNumber: 10 })
  })

  it('sends the edited status and category of an order that was loaded from the API', async () => {
    apiGet.mockResolvedValue({
      AbsoluteEntry: 646,
      DocumentNumber: 10,
      ItemNo: 'FG-001',
      ProductionOrderStatus: 'boposPlanned',
      U_ProdType: 'INT',
      U_DwgNo: 'DWG-1',
      PlannedQuantity: 12,
      CompletedQuantity: 3,
      PostingDate: '2026-06-16T00:00:00Z',
      Warehouse: 'WIP',
      ProductionOrderLines: [{ LineNumber: 0, ItemNo: 'RM-100', PlannedQuantity: 24, Warehouse: 'Store1' }],
    })

    const loaded = await getProductionOrder(646)
    await updateProductionOrder(646, {
      ...loaded,
      Status: 'boposReleased',
      ProductionCategory: 'JOB',
      DrawingNo: 'DWG-2',
    })

    expect(apiPut.mock.calls[0][0]).toBe('/production-orders/646')
    const body = bodyOf(apiPut)
    expect(body.ProductionOrderStatus).toBe('boposReleased')
    expect(body.U_ProdType).toBe('JOB')
    expect(body.U_DwgNo).toBe('DWG-2')
    expect(body.ItemNo).toBe('FG-001')
    expect(body.CompletedQuantity).toBe(3)
    expect(body.AbsoluteEntry).toBe(646)
  })
})
