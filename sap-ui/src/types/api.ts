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
