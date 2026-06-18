import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { API_BASE_URL, STORAGE_KEYS } from '@/config/constants'
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

function clearStoredAuth() {
  localStorage.removeItem(STORAGE_KEYS.TOKEN)
  localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
  localStorage.removeItem(STORAGE_KEYS.USER)
}

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN)
  if (!refreshToken) return null

  try {
    const response = await refreshTokenApi(refreshToken)
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
    const token = localStorage.getItem(STORAGE_KEYS.TOKEN)
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error: AxiosError) => Promise.reject(error),
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
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetriableRequest | undefined
    const isAuthRoute = originalRequest?.url?.includes('/auth/login')
      || originalRequest?.url?.includes('/auth/refresh')
      || originalRequest?.url?.includes('/auth/register')

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry && !isAuthRoute) {
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

    if (error.response?.status === 401) {
      clearStoredAuth()
      if (!window.location.pathname.startsWith('/auth')) {
        window.location.href = '/auth/login'
      }
    }

    return Promise.reject(error)
  },
)

export default axiosInstance
