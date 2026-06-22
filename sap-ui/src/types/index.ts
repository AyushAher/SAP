export interface User {
  id: string
  email: string
  name: string
  role: string
  roles?: string[]
  companyDb?: string
  branchId?: number | null
}

export interface AuthState {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  companyDb: string | null
  branchId: number | null
}

export interface SelectOption {
  value: string
  label: string
  disabled?: boolean
}

export interface ApiError {
  message: string
  status?: number
  code?: string
}

export interface LoginCredentials {
  userName: string
  password: string
  companyDb: string
}

export type {
  FilterOperator,
  Filter,
  Sort,
  PaginationRequest,
  ApiResponse,
  PaginationResponse,
} from './api'
