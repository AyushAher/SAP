import type { Filter, PaginationRequest, PaginationResponse, Sort } from '@/types/api'

export const DEFAULT_PAGE_SIZE = 10
export const DEFAULT_PAGE_SIZE_OPTIONS = [5, 10, 20, 50, 100]

export function createDefaultPaginationRequest(
  overrides?: Partial<PaginationRequest>,
): PaginationRequest {
  return {
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    filters: [],
    sorts: [],
    ...overrides,
  }
}

export function createPaginationResponse<T>(
  data: T,
  request: PaginationRequest,
  options?: {
    success?: boolean
    errorCode?: string
    message?: string
    totalCount?: number
  },
): PaginationResponse<T> {
  return {
    success: options?.success ?? true,
    errorCode: options?.errorCode,
    message: options?.message,
    data,
    pageSize: request.pageSize,
    pageNumber: request.pageNumber,
    filters: request.filters,
    sorts: request.sorts,
    totalCount: options?.totalCount,
  }
}

export function createApiResponse<T>(
  data: T,
  options?: { success?: boolean; errorCode?: string; message?: string },
): { success: boolean; errorCode?: string; message?: string; data: T } {
  return {
    success: options?.success ?? true,
    errorCode: options?.errorCode,
    message: options?.message,
    data,
  }
}

function matchesFilter(value: unknown, filter: Filter): boolean {
  if (value === null || value === undefined) return false

  const strValue = String(value).toLowerCase()
  const filterValue = filter.value

  switch (filter.operator) {
    case 'eq':
      return strValue === String(filterValue).toLowerCase()
    case 'neq':
      return strValue !== String(filterValue).toLowerCase()
    case 'contains':
      return strValue.includes(String(filterValue).toLowerCase())
    case 'startsWith':
      return strValue.startsWith(String(filterValue).toLowerCase())
    case 'endsWith':
      return strValue.endsWith(String(filterValue).toLowerCase())
    case 'gt':
      return Number(value) > Number(filterValue)
    case 'gte':
      return Number(value) >= Number(filterValue)
    case 'lt':
      return Number(value) < Number(filterValue)
    case 'lte':
      return Number(value) <= Number(filterValue)
    case 'in':
      return Array.isArray(filterValue)
        ? filterValue.map(String).includes(String(value))
        : false
    default:
      return true
  }
}

function compareValues(a: unknown, b: unknown): number {
  if (a === b) return 0
  if (a === null || a === undefined) return 1
  if (b === null || b === undefined) return -1

  if (typeof a === 'number' && typeof b === 'number') return a - b
  return String(a).localeCompare(String(b), undefined, { sensitivity: 'base' })
}

export function applyPaginationRequest<T>(
  items: T[],
  request: PaginationRequest,
  accessors?: Partial<Record<string, (row: T) => unknown>>,
): { items: T[]; totalCount: number } {
  let result = [...items]

  const getFieldValue = (row: T, field: string): unknown => {
    const accessor = accessors?.[field]
    if (accessor) return accessor(row)
    return (row as Record<string, unknown>)[field]
  }

  if (request.filters.length > 0) {
    result = result.filter((row) =>
      request.filters.every((filter) => {
        const value = getFieldValue(row, filter.field)
        return matchesFilter(value, filter)
      }),
    )
  }

  if (request.sorts.length > 0) {
    result.sort((a, b) => {
      for (const sort of request.sorts) {
        const aVal = getFieldValue(a, sort.field)
        const bVal = getFieldValue(b, sort.field)
        const cmp = compareValues(aVal, bVal)
        if (cmp !== 0) return sort.direction === 'asc' ? cmp : -cmp
      }
      return 0
    })
  }

  const totalCount = result.length
  const pageSize = request.pageSize ?? DEFAULT_PAGE_SIZE
  const start = (request.pageNumber - 1) * pageSize
  const paginated = result.slice(start, start + pageSize)

  return { items: paginated, totalCount }
}

export function toggleSort(sorts: Sort[], field: string): Sort[] {
  const existing = sorts.find((s) => s.field === field)

  if (!existing) {
    return [{ field, direction: 'asc' }]
  }

  if (existing.direction === 'asc') {
    return [{ field, direction: 'desc' }]
  }

  return []
}

export function getSortForField(sorts: Sort[], field: string): Sort | undefined {
  return sorts.find((s) => s.field === field)
}
