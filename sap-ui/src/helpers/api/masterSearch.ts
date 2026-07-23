import type { PaginationRequest } from '@/types/api'
import { DEFAULT_PAGE_SIZE } from '@/helpers/api/pagination'

export function createMasterSearchRequest(
  search: string,
  options?: { pageSize?: number; pageNumber?: number; fields?: string[] },
): PaginationRequest {
  const trimmed = search.trim()
  return {
    pageNumber: options?.pageNumber ?? 1,
    pageSize: options?.pageSize ?? DEFAULT_PAGE_SIZE,
    filters: trimmed
      ? [{ field: '__search', operator: 'contains', value: trimmed }]
      : [],
    sorts: [],
    fields: options?.fields,
  }
}
