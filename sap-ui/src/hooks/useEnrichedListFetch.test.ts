import { renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

vi.mock('@/helpers/masterLookup', () => ({
  buildMasterLookupMapsFromRows: vi.fn(async () => ({
    items: {},
    projects: {},
    businessPartners: {},
  })),
}))

interface Row {
  project?: string
}

const emptyPage: PaginationResponse<Row[]> = { success: true, data: [] }

describe('useEnrichedListFetch', () => {
  it('keeps fetchData stable when extractors are passed as an inline object', () => {
    // DataTable refetches whenever fetchData changes identity, so an unstable fetchData means an
    // endless fetch/render loop (spinner never settles).
    const fetchFn = (_request: PaginationRequest) => Promise.resolve(emptyPage)

    const { result, rerender } = renderHook(() =>
      useEnrichedListFetch<Row>(fetchFn, { projectCodes: (row) => row.project }),
    )

    const first = result.current.fetchData
    rerender()

    expect(result.current.fetchData).toBe(first)
  })

  it('gives a new fetchData when the underlying fetch function changes', () => {
    const { result, rerender } = renderHook(
      ({ fetchFn }: { fetchFn: (request: PaginationRequest) => Promise<PaginationResponse<Row[]>> }) =>
        useEnrichedListFetch<Row>(fetchFn, { projectCodes: (row) => row.project }),
      { initialProps: { fetchFn: () => Promise.resolve(emptyPage) } },
    )

    const first = result.current.fetchData
    rerender({ fetchFn: () => Promise.resolve(emptyPage) })

    expect(result.current.fetchData).not.toBe(first)
  })
})
