import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as apiClient from '@/helpers/api/client'
import * as apiList from '@/helpers/api/list'
import {
  cancelProductionOrder,
  createProductionOrder,
  getProductionOrder,
  listProductionOrders,
  listSubassemblies,
  updateProductionOrder,
} from './productionOrders'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  invalidateCachedGets: vi.fn(),
}))

vi.mock('@/helpers/api/list', () => ({
  apiListPost: vi.fn().mockResolvedValue({ data: [], totalCount: 0 }),
}))

const apiGet = vi.mocked(apiClient.apiGet)
const apiPost = vi.mocked(apiClient.apiPost)
const apiPut = vi.mocked(apiClient.apiPut)
const apiListPost = vi.mocked(apiList.apiListPost)

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
      ParentProductionOrderNo: '10',
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
    expect(body.ProductionOrderType).toBe('bopotSpecial')
    expect(body.U_ProdType).toBe('JOB')
    expect(body.U_DwgNo).toBe('DWG-7')
    expect(body.U_DocNum).toBe('10')
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

describe('listProductionOrders', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiListPost.mockResolvedValue({ data: [], totalCount: 0, pageNumber: 1, pageSize: 20 } as never)
  })

  it('excludes child sub-assemblies from the main list by default', async () => {
    await listProductionOrders({ pageNumber: 1, pageSize: 20, filters: [], sorts: [] })

    expect(apiListPost.mock.calls[0][0]).toBe('/production-orders/list?excludeSubassemblies=true')
  })

  it('includes children when the production picker asks for them', async () => {
    await listProductionOrders({ pageNumber: 1, pageSize: 20, filters: [], sorts: [] }, { excludeSubassemblies: false })

    expect(apiListPost.mock.calls[0][0]).toBe('/production-orders/list?excludeSubassemblies=false')
  })
})

describe('listSubassemblies', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    apiGet.mockResolvedValue([
      { AbsoluteEntry: 700, DocumentNumber: 21, ItemNo: 'SA-001', U_DocNum: '10' },
    ])
  })

  it('loads children of the parent and maps the parent UDF', async () => {
    const rows = await listSubassemblies(646)

    expect(apiGet).toHaveBeenCalledWith('/production-orders/646/subassemblies')
    expect(rows).toHaveLength(1)
    expect(rows[0].ItemNumber).toBe('SA-001')
    expect(rows[0].ParentProductionOrderNo).toBe('10')
  })

  it('asks the API for cancelled siblings when allocating the next number', async () => {
    await listSubassemblies(646, { includeCancelled: true })

    expect(apiGet).toHaveBeenCalledWith('/production-orders/646/subassemblies?includeCancelled=true')
  })
})

describe('cancelProductionOrder', () => {
  it('posts to the cancel endpoint', async () => {
    apiPost.mockResolvedValue({ AbsoluteEntry: 700, DocumentNumber: 21 })

    await cancelProductionOrder(700)

    expect(apiPost.mock.calls[0][0]).toBe('/production-orders/700/cancel')
  })
})
