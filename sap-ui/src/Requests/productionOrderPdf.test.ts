import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as apiClient from '@/helpers/api/client'
import { downloadProductionOrderPdf } from './productionOrders'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDownloadGet: vi.fn(),
  invalidateCachedGets: vi.fn(),
}))

const apiDownloadGet = vi.mocked(apiClient.apiDownloadGet)

function stubObjectUrl() {
  const createObjectURL = vi.fn(() => 'blob:production-order')
  const revokeObjectURL = vi.fn()
  Object.assign(URL, { createObjectURL, revokeObjectURL })
  return { createObjectURL, revokeObjectURL }
}

describe('downloadProductionOrderPdf', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    stubObjectUrl()
  })

  it('asks the API for the production order document and names the file after the doc number', async () => {
    apiDownloadGet.mockResolvedValue(new Blob(['%PDF-1.7'], { type: 'application/pdf' }))
    const anchor = document.createElement('a')
    const createElement = vi.spyOn(document, 'createElement').mockReturnValue(anchor)
    const click = vi.spyOn(anchor, 'click').mockImplementation(() => {})

    await downloadProductionOrderPdf(646, 10)

    expect(apiDownloadGet).toHaveBeenCalledWith('/production-orders/646/pdf')
    expect(anchor.download).toBe('ProductionOrder(10).pdf')
    expect(click).toHaveBeenCalledTimes(1)
    createElement.mockRestore()
  })

  it('falls back to the entry when the row has no document number', async () => {
    apiDownloadGet.mockResolvedValue(new Blob(['%PDF-1.7'], { type: 'application/pdf' }))
    const anchor = document.createElement('a')
    const createElement = vi.spyOn(document, 'createElement').mockReturnValue(anchor)
    vi.spyOn(anchor, 'click').mockImplementation(() => {})

    await downloadProductionOrderPdf(646)

    expect(anchor.download).toBe('ProductionOrder(646).pdf')
    createElement.mockRestore()
  })

  it('surfaces the error envelope the API returned inside the blob', async () => {
    apiDownloadGet.mockRejectedValue({
      response: {
        status: 404,
        data: new Blob([JSON.stringify({ success: false, message: 'Production order not found' })]),
      },
    })

    await expect(downloadProductionOrderPdf(999)).rejects.toThrow('Production order not found')
  })

  it('never downloads a file when the request failed', async () => {
    apiDownloadGet.mockRejectedValue(new Error('Network Error'))
    const click = vi.fn()
    const createElement = vi.spyOn(document, 'createElement')
      .mockReturnValue({ click } as unknown as HTMLAnchorElement)

    await expect(downloadProductionOrderPdf(646)).rejects.toThrow('Network Error')
    expect(click).not.toHaveBeenCalled()
    createElement.mockRestore()
  })
})
