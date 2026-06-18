import { useCallback, useEffect, useRef, useState } from 'react'
import {
  createDefaultPaginationRequest,
  DEFAULT_PAGE_SIZE,
  toggleSort,
} from '@/helpers/api/pagination'
import type { Filter, FilterOperator, PaginationRequest, PaginationResponse, Sort } from '@/types/api'

export interface UseDataTableOptions<T> {
  fetchData: (request: PaginationRequest) => Promise<PaginationResponse<T[]>>
  defaultPageSize?: number
  initialFilters?: Filter[]
  initialSorts?: Sort[]
  filterDebounceMs?: number
}

export interface UseDataTableReturn<T> {
  data: T[]
  loading: boolean
  error: string | null
  request: PaginationRequest
  totalCount: number
  pageCount: number
  filterValues: Record<string, string>
  setPage: (page: number) => void
  setPageSize: (size: number) => void
  setFilter: (field: string, value: string, operator?: FilterOperator) => void
  clearFilters: () => void
  toggleColumnSort: (field: string) => void
  refresh: () => void
  getSortDirection: (field: string) => 'asc' | 'desc' | null
}

export function useDataTable<T>({
  fetchData,
  defaultPageSize = DEFAULT_PAGE_SIZE,
  initialFilters = [],
  initialSorts = [],
  filterDebounceMs = 300,
}: UseDataTableOptions<T>): UseDataTableReturn<T> {
  const [data, setData] = useState<T[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [totalCount, setTotalCount] = useState(0)
  const [filterValues, setFilterValues] = useState<Record<string, string>>({})
  const [request, setRequest] = useState<PaginationRequest>(() =>
    createDefaultPaginationRequest({
      pageSize: defaultPageSize,
      filters: initialFilters,
      sorts: initialSorts,
    }),
  )

  const filterOperators = useRef<Record<string, FilterOperator>>({})
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const fetchId = useRef(0)

  const loadData = useCallback(async (req: PaginationRequest) => {
    const currentFetchId = ++fetchId.current
    setLoading(true)
    setError(null)

    try {
      const response = await fetchData(req)

      if (currentFetchId !== fetchId.current) return

      if (!response.success) {
        setData([])
        setTotalCount(0)
        setError(response.message ?? response.errorCode ?? 'Failed to load data')
        return
      }

      const items = response.data ?? []
      setData(items)
      setTotalCount(
        response.totalCount ??
          (items.length < (req.pageSize ?? DEFAULT_PAGE_SIZE)
            ? (req.pageNumber - 1) * (req.pageSize ?? DEFAULT_PAGE_SIZE) + items.length
            : req.pageNumber * (req.pageSize ?? DEFAULT_PAGE_SIZE) + 1),
      )
    } catch (err) {
      if (currentFetchId !== fetchId.current) return
      setData([])
      setTotalCount(0)
      setError(err instanceof Error ? err.message : 'An unexpected error occurred')
    } finally {
      if (currentFetchId === fetchId.current) {
        setLoading(false)
      }
    }
  }, [fetchData])

  useEffect(() => {
    loadData(request)
  }, [request, loadData])

  const buildFiltersFromValues = useCallback(
    (values: Record<string, string>): Filter[] => {
      return Object.entries(values)
        .filter(([, value]) => value.trim() !== '')
        .map(([field, value]) => ({
          field,
          operator: filterOperators.current[field] ?? 'contains',
          value,
        }))
    },
    [],
  )

  const applyFilterUpdate = useCallback(
    (field: string, value: string, operator?: FilterOperator) => {
      if (operator) {
        filterOperators.current[field] = operator
      }

      setFilterValues((prev) => {
        const next = { ...prev, [field]: value }
        if (!value.trim()) delete next[field]

        if (debounceTimer.current) clearTimeout(debounceTimer.current)

        debounceTimer.current = setTimeout(() => {
          setRequest((prev) => ({
            ...prev,
            pageNumber: 1,
            filters: buildFiltersFromValues(next),
          }))
        }, filterDebounceMs)

        return next
      })
    },
    [buildFiltersFromValues, filterDebounceMs],
  )

  const setPage = useCallback((page: number) => {
    setRequest((prev) => ({ ...prev, pageNumber: page }))
  }, [])

  const setPageSize = useCallback((size: number) => {
    setRequest((prev) => ({ ...prev, pageSize: size, pageNumber: 1 }))
  }, [])

  const clearFilters = useCallback(() => {
    if (debounceTimer.current) clearTimeout(debounceTimer.current)
    filterOperators.current = {}
    setFilterValues({})
    setRequest((prev) => ({ ...prev, pageNumber: 1, filters: [] }))
  }, [])

  const toggleColumnSort = useCallback((field: string) => {
    setRequest((prev) => ({
      ...prev,
      pageNumber: 1,
      sorts: toggleSort(prev.sorts, field),
    }))
  }, [])

  const refresh = useCallback(() => {
    loadData(request)
  }, [loadData, request])

  const getSortDirection = useCallback(
    (field: string): 'asc' | 'desc' | null => {
      const sort = request.sorts.find((s) => s.field === field)
      return sort?.direction ?? null
    },
    [request.sorts],
  )

  const pageSize = request.pageSize ?? defaultPageSize
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))

  return {
    data,
    loading,
    error,
    request,
    totalCount,
    pageCount,
    filterValues,
    setPage,
    setPageSize,
    setFilter: applyFilterUpdate,
    clearFilters,
    toggleColumnSort,
    refresh,
    getSortDirection,
  }
}
