import axiosInstance, { getApiErrorMessage } from '@/helpers/api/axiosInstance'
import type { ApiResponse } from '@/types/api'

export async function apiGet<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  try {
    const { data } = await axiosInstance.get<ApiResponse<T>>(url, { params })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiPost<T>(url: string, body?: unknown, params?: Record<string, unknown>): Promise<T> {
  try {
    const { data } = await axiosInstance.post<ApiResponse<T>>(url, body, { params })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiPut<T>(url: string, body?: unknown, params?: Record<string, unknown>): Promise<T> {
  try {
    const { data } = await axiosInstance.put<ApiResponse<T>>(url, body, { params })
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiDelete<T>(url: string): Promise<T> {
  try {
    const { data } = await axiosInstance.delete<ApiResponse<T>>(url)
    if (!data.success) throw new Error(data.message ?? data.errorCode ?? 'Request failed')
    return data.data as T
  } catch (error) {
    throw new Error(getApiErrorMessage(error))
  }
}

export async function apiDownload(url: string, body?: unknown): Promise<Blob> {
  const response = await axiosInstance.post(url, body, { responseType: 'blob' })
  return response.data as Blob
}

export async function apiDownloadGet(url: string): Promise<Blob> {
  const response = await axiosInstance.get(url, { responseType: 'blob' })
  return response.data as Blob
}
