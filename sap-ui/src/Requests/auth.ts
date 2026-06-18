import { apiPost, apiGet } from '@/helpers/api/client'
import { rsaEncrypt } from '@/helpers/lib/rsa'

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

export async function refreshTokenApi(refreshToken: string) {
  return apiPost<LoginApiResponse>('/auth/refresh', { refreshToken })
}

export function getLoginClaims(response: LoginApiResponse): AuthClaim[] {
  return response.Claims ?? response.claims ?? []
}

export async function loginApi(userName: string, password: string) {
  const encrypted = rsaEncrypt(password)
  return apiPost<LoginApiResponse>('/auth/login', { userName, password: encrypted })
}

export async function registerApi(payload: {
  fullName: string
  userName: string
  email: string
  password: string
}) {
  const encrypted = rsaEncrypt(payload.password)
  return apiPost('/auth/register', {
    fullName: payload.fullName,
    userName: payload.userName,
    email: payload.email,
    password: encrypted,
  })
}

export async function getPublicKey() {
  return apiGet<{ publicKey: string }>('/auth/public-key')
}
