import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { API_BASE_URL, API_ERROR_CODES, ROUTES, STORAGE_KEYS } from '@/config/constants'
import { decrementApiLoading, incrementApiLoading } from '@/helpers/api/apiLoading'
import { getLoginRefreshToken, getLoginToken, refreshTokenApi } from '@/Requests/auth'
import type { ApiResponse } from '@/types/api'

const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
})

type RetriableRequest = InternalAxiosRequestConfig & { _retry?: boolean }

let refreshInFlight: Promise<string | null> | null = null

function shouldSkipLogoutOn401(url?: string): boolean {
  if (!url) return false
  return url.includes('/auth/login')
    || url.includes('/auth/refresh')
    || url.includes('/auth/register')
    || url.includes('/auth/switch-company')
    || url.includes('/auth/switch-branch')
    || url.includes('/auth/branches')
}

function isSapSessionExpired(error: AxiosError): boolean {
  const body = error.response?.data as ApiResponse | undefined
  return body?.errorCode === API_ERROR_CODES.SAP_SESSION_UNAVAILABLE
}

function forceSessionExpiredRedirect() {
  clearStoredAuth()
  window.dispatchEvent(new Event('auth:session-expired'))
}

function redirectToLogin() {
  forceSessionExpiredRedirect()
  if (!window.location.pathname.startsWith('/auth')) {
    window.location.href = ROUTES.LOGIN
  }
}

function readStoredBranchId(): number | null {
  const raw = localStorage.getItem(STORAGE_KEYS.BRANCH_ID)
  if (!raw) return null
  const parsed = Number(raw)
  return Number.isFinite(parsed) ? parsed : null
}

function clearStoredAuth() {
  localStorage.removeItem(STORAGE_KEYS.TOKEN)
  localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
  localStorage.removeItem(STORAGE_KEYS.USER)
  localStorage.removeItem(STORAGE_KEYS.COMPANY_DB)
  localStorage.removeItem(STORAGE_KEYS.BRANCH_ID)
}

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN)
  const companyDb = localStorage.getItem(STORAGE_KEYS.COMPANY_DB)
  if (!refreshToken || !companyDb) return null

  try {
    const response = await refreshTokenApi(refreshToken, companyDb, readStoredBranchId())
    const token = getLoginToken(response)
    const nextRefreshToken = getLoginRefreshToken(response)
    if (!token) return null

    localStorage.setItem(STORAGE_KEYS.TOKEN, token)
    if (nextRefreshToken) {
      localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, nextRefreshToken)
    }
    return token
  } catch {
    return null
  }
}

axiosInstance.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    incrementApiLoading()
    const token = localStorage.getItem(STORAGE_KEYS.TOKEN)
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error: AxiosError) => {
    decrementApiLoading()
    return Promise.reject(error)
  },
)

export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const body = error.response?.data as ApiResponse | undefined
    if (body?.message) return body.message
    if (body?.errorCode) return body.errorCode
    if (error.response?.status === 401) return 'Invalid credentials'
    if (error.message) return error.message
  }
  if (error instanceof Error) return error.message
  return 'Request failed'
}

axiosInstance.interceptors.response.use(
  (response) => {
    decrementApiLoading()
    return response
  },
  async (error: AxiosError) => {
    decrementApiLoading()
    const originalRequest = error.config as RetriableRequest | undefined
    const skipLogout = shouldSkipLogoutOn401(originalRequest?.url)

    if (isSapSessionExpired(error) && !skipLogout) {
      redirectToLogin()
      return Promise.reject(error)
    }

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry && !skipLogout && !isSapSessionExpired(error)) {
      originalRequest._retry = true
      refreshInFlight ??= refreshAccessToken().finally(() => {
        refreshInFlight = null
      })

      const newToken = await refreshInFlight
      if (newToken) {
        originalRequest.headers.Authorization = `Bearer ${newToken}`
        return axiosInstance(originalRequest)
      }
    }

    if (error.response?.status === 401 && !skipLogout) {
      redirectToLogin()
    }

    return Promise.reject(error)
  },
)

export default axiosInstance
