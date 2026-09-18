import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as masters from '@/Requests/masters'
import {
  ensureItemList,
  resetItemSearchCache,
  searchItemsCached,
} from './itemSearchCache'

vi.mock('@/Requests/masters', async () => {
  const actual = await vi.importActual<typeof import('@/Requests/masters')>('@/Requests/masters')
  return {
    ...actual,
    searchItems: vi.fn(),
  }
})

vi.mock('@/helpers/masterLookup', async () => {
  const actual = await vi.importActual<typeof import('@/helpers/masterLookup')>('@/helpers/masterLookup')
  return {
    ...actual,
    rememberMasterItem: vi.fn(),
  }
})

const searchItems = vi.mocked(masters.searchItems)

describe('itemSearchCache', () => {
  beforeEach(() => {
    resetItemSearchCache()
    searchItems.mockReset()
    searchItems.mockResolvedValue({
      data: [
        { ItemCode: 'CHANNEL-200', ItemName: 'Channel', InventoryUom: 'NOS' },
        { ItemCode: 'BEAM-100', ItemName: 'Beam', InventoryUom: 'NOS' },
      ],
    } as never)
  })

  it('loads the item list from SAP once and filters later searches locally', async () => {
    await ensureItemList()
    const first = await searchItemsCached('chan')
    const second = await searchItemsCached('beam')

    expect(searchItems).toHaveBeenCalledTimes(1)
    expect(first.data.map((row) => row.ItemCode)).toEqual(['CHANNEL-200'])
    expect(second.data.map((row) => row.ItemCode)).toEqual(['BEAM-100'])
  })

  it('falls back to SAP when the cached list does not contain the typed item', async () => {
    await ensureItemList()
    searchItems.mockResolvedValueOnce({
      data: [{ ItemCode: 'RARE-9', ItemName: 'Rare fitting' }],
    } as never)

    const result = await searchItemsCached('rare-9')

    expect(searchItems).toHaveBeenCalledTimes(2)
    expect(result.data.map((row) => row.ItemCode)).toEqual(['RARE-9'])
  })
})
