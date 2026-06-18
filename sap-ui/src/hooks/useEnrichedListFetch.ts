import { useCallback, useState } from 'react'
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

export function useEnrichedListFetch<T>(
  fetchFn: (request: PaginationRequest) => Promise<PaginationResponse<T[]>>,
  extractors: {
    itemCodes?: (row: T) => string | undefined
    projectCodes?: (row: T) => string | undefined
    cardCodes?: (row: T) => string | undefined
  },
) {
  const [lookupMaps, setLookupMaps] = useState<MasterLookupMaps>(emptyMaps)

  const fetchData = useCallback(async (request: PaginationRequest) => {
    const response = await fetchFn(request)
    const rows = response.data ?? []
    if (!rows.length) {
      setLookupMaps(emptyMaps)
      return response
    }

    try {
      const maps = await buildMasterLookupMapsFromRows(rows, extractors)
      setLookupMaps(maps)
    } catch {
      setLookupMaps(emptyMaps)
    }

    return response
  }, [fetchFn, extractors.itemCodes, extractors.projectCodes, extractors.cardCodes])

  return { fetchData, lookupMaps }
}
