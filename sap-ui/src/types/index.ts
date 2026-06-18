export interface User {
  id: string
  email: string
  name: string
  role: string
  roles?: string[]
}

export interface AuthState {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
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
}

export type {
  FilterOperator,
  Filter,
  Sort,
  PaginationRequest,
  ApiResponse,
  PaginationResponse,
} from './api'
