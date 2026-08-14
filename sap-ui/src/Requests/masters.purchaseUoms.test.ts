import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as apiClient from '@/helpers/api/client'
import * as listApi from '@/helpers/api/list'
import { listPurchaseUoms, lookupHsnLabels, lookupSacLabels } from './masters'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
}))

vi.mock('@/helpers/api/list', () => ({
  apiListPost: vi.fn(),
}))

const apiGet = vi.mocked(apiClient.apiGet)
const apiListPost = vi.mocked(listApi.apiListPost)

describe('listPurchaseUoms', () => {
  beforeEach(() => vi.clearAllMocks())

  it('asks SAP for the units of one item', async () => {
    apiGet.mockResolvedValue([])
    await listPurchaseUoms('RM/570 3813', 'kg')
    expect(apiGet).toHaveBeenCalledWith('/masters/items/RM%2F570%203813/purchase-uoms?search=kg')
  })

  it('keeps the UoM group entry and conversion factor for group-based units', async () => {
    apiGet.mockResolvedValue([
      { Code: 'BOX', Name: 'BOXES', ItemsPerUnit: 12, UoMEntry: 4, IsDefault: true, Source: 'group' },
    ])
    const [uom] = await listPurchaseUoms('FG-001')
    expect(uom).toEqual({
      Code: 'BOX',
      Name: 'BOXES',
      ItemsPerUnit: 12,
      UoMEntry: 4,
      IsDefault: true,
      Source: 'group',
    })
  })

  it('leaves the factor unset for master units SAP cannot convert', async () => {
    apiGet.mockResolvedValue([{ Code: 'KGS', Name: 'KILOGRAMS', ItemsPerUnit: null, Source: 'master' }])
    const [uom] = await listPurchaseUoms('RM-1')
    expect(uom.ItemsPerUnit).toBeUndefined()
    expect(uom.UoMEntry).toBeUndefined()
    expect(uom.Source).toBe('master')
  })

  it('treats JSON nulls from the camelCase API as unset, not zero', async () => {
    apiGet.mockResolvedValue([
      { code: 'LTR', name: 'LITRES', uomEntry: null, itemsPerUnit: null, isDefault: false, source: 'master' },
    ])
    const [uom] = await listPurchaseUoms('RM-1')
    expect(uom).toEqual({
      Code: 'LTR',
      Name: 'LITRES',
      ItemsPerUnit: undefined,
      UoMEntry: undefined,
      IsDefault: false,
      Source: 'master',
    })
  })

  it('returns nothing without an item, and never calls the API', async () => {
    expect(await listPurchaseUoms('  ')).toEqual([])
    expect(apiGet).not.toHaveBeenCalled()
  })

  it('degrades to an empty list when the lookup fails', async () => {
    apiGet.mockRejectedValue(new Error('SAP down'))
    expect(await listPurchaseUoms('RM-1')).toEqual([])
  })
})

describe('lookupHsnLabels / lookupSacLabels', () => {
  beforeEach(() => vi.clearAllMocks())

  it('labels a saved HSN entry with its code and description', async () => {
    apiListPost.mockResolvedValue({
      data: [
        { AbsEntry: 190, ChapterID: '84.19.00', Description: 'BOILERS' },
        { AbsEntry: 19, ChapterID: '72.16.32', Description: 'ANGLES, BEAMS, CHANNELS, FLAT' },
      ],
    } as never)
    expect(await lookupHsnLabels([19])).toEqual({ 19: '72.16.32 - ANGLES, BEAMS, CHANNELS, FLAT' })
  })

  it('labels a saved SAC entry from ServiceName', async () => {
    apiListPost.mockResolvedValue({
      data: [{ AbsEntry: 11, ServiceCode: '998335', ServiceName: 'ENGINEERING SERVICES' }],
    } as never)
    expect(await lookupSacLabels([11])).toEqual({ 11: '998335 - ENGINEERING SERVICES' })
  })

  it('asks once per distinct entry and skips entries SAP does not return', async () => {
    apiListPost.mockResolvedValue({ data: [] } as never)
    expect(await lookupHsnLabels([19, 19, 20])).toEqual({})
    expect(apiListPost).toHaveBeenCalledTimes(2)
  })

  it('does nothing when there is nothing to label', async () => {
    expect(await lookupSacLabels([])).toEqual({})
    expect(apiListPost).not.toHaveBeenCalled()
  })
})
