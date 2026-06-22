import { apiPost, apiGet } from '@/helpers/api/client'
import { rsaEncrypt } from '@/helpers/lib/rsa'
import type { SapCompanyDatabase } from '@/config/companyDb'

export interface AuthClaim {
  type?: string
  value?: string
  Type?: string
  Value?: string
}

export interface LoginApiResponse {
  Token?: string
  token?: string
  RefreshToken?: string
  refreshToken?: string
  Claims?: AuthClaim[]
  claims?: AuthClaim[]
}

export function getLoginToken(response: LoginApiResponse): string | undefined {
  return response.Token ?? response.token
}

export function getLoginRefreshToken(response: LoginApiResponse): string | undefined {
  return response.RefreshToken ?? response.refreshToken
}

export async function refreshTokenApi(refreshToken: string, companyDb: string, branchId?: number | null) {
  return apiPost<LoginApiResponse>('/auth/refresh', { refreshToken, companyDb, branchId: branchId ?? null })
}

export function getLoginClaims(response: LoginApiResponse): AuthClaim[] {
  return response.Claims ?? response.claims ?? []
}

export async function loginApi(userName: string, password: string, companyDb: string) {
  const encrypted = rsaEncrypt(password)
  return apiPost<LoginApiResponse>('/auth/login', { userName, password: encrypted, companyDb })
}

export async function registerApi(payload: {
  fullName: string
  userName: string
  email: string
  password: string
  companyDb: string
}) {
  const encrypted = rsaEncrypt(payload.password)
  return apiPost('/auth/register', {
    fullName: payload.fullName,
    userName: payload.userName,
    email: payload.email,
    password: encrypted,
    companyDb: payload.companyDb,
  })
}

export async function switchCompanyApi(companyDb: SapCompanyDatabase, password: string) {
  const encrypted = rsaEncrypt(password)
  return apiPost<LoginApiResponse>('/auth/switch-company', { companyDb, password: encrypted })
}

export interface BranchOption {
  id: number
  name: string
}

export async function getBranchesApi() {
  return apiGet<BranchOption[]>('/auth/branches')
}

export async function switchBranchApi(branchId: number | null) {
  return apiPost<LoginApiResponse>('/auth/switch-branch', { branchId })
}

export async function logoutApi() {
  return apiPost('/auth/logout', {})
}

export async function getPublicKey() {
  return apiGet<{ publicKey: string }>('/auth/public-key')
}

export async function getCompanyDatabasesApi() {
  return apiGet<string[]>('/auth/company-databases')
}
