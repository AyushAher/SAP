export type FilterOperator =
  | 'eq'
  | 'neq'
  | 'contains'
  | 'startsWith'
  | 'endsWith'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'in'

export interface Filter {
  field: string
  operator: FilterOperator
  value: string | number | boolean | string[]
}

export interface Sort {
  field: string
  direction: 'asc' | 'desc'
}

export interface PaginationRequest {
  pageSize?: number
  pageNumber: number
  filters: Filter[]
  sorts: Sort[]
  /**
   * Optional subset of field names actually needed by the caller (e.g. `['ItemCode', 'ItemName']` for
   * a dropdown that only displays a code + label). When omitted, the backend falls back to its default
   * field set for that endpoint. Fields outside the endpoint's known set are ignored server-side.
   */
  fields?: string[]
}

export interface ApiResponse<T = unknown> {
  success: boolean
  errorCode?: string
  message?: string
  data?: T
}

export interface PaginationResponse<T = unknown> extends ApiResponse<T> {
  pageSize?: number
  pageNumber: number
  filters: Filter[]
  sorts: Sort[]
  totalCount?: number
}
