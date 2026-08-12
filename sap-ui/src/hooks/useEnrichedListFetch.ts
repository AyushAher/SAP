import { useCallback, useRef, useState } from 'react'
import {
  buildMasterLookupMapsFromRows,
  type MasterLookupMaps,
} from '@/helpers/masterLookup'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

const emptyMaps: MasterLookupMaps = {
  items: {},
  projects: {},
  businessPartners: {},
}

export interface ListRowExtractors<T> {
  itemCodes?: (row: T) => string | undefined
  projectCodes?: (row: T) => string | undefined
  cardCodes?: (row: T) => string | undefined
}

export function useEnrichedListFetch<T>(
  fetchFn: (request: PaginationRequest) => Promise<PaginationResponse<T[]>>,
  extractors: ListRowExtractors<T>,
) {
  const [lookupMaps, setLookupMaps] = useState<MasterLookupMaps>(emptyMaps)

  // Read extractors through a ref so callers may pass an inline object literal: including the
  // extractor functions in the dependency list below would give fetchData a new identity on every
  // render, and DataTable refetches whenever fetchData changes — an endless fetch/render loop.
  const extractorsRef = useRef(extractors)
  extractorsRef.current = extractors

  const fetchData = useCallback(async (request: PaginationRequest) => {
    const response = await fetchFn(request)
    const rows = response.data ?? []
    if (!rows.length) {
      setLookupMaps(emptyMaps)
      return response
    }

    try {
      const maps = await buildMasterLookupMapsFromRows(rows, extractorsRef.current)
      setLookupMaps(maps)
    } catch {
      setLookupMaps(emptyMaps)
    }

    return response
  }, [fetchFn])

  return { fetchData, lookupMaps }
}
