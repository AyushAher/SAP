import { describe, expect, it, beforeEach, vi } from 'vitest'
import { configureStore } from '@reduxjs/toolkit'
import authReducer, { login, logout, switchCompany } from './authSlice'
import { STORAGE_KEYS } from '@/config/constants'
import * as authApi from '@/Requests/auth'

vi.mock('@/Requests/auth', () => ({
  loginApi: vi.fn(),
  switchCompanyApi: vi.fn(),
  getLoginToken: (response: { Token?: string }) => response.Token,
  getLoginRefreshToken: (response: { RefreshToken?: string }) => response.RefreshToken,
  getLoginClaims: (response: { Claims?: Array<{ Type: string; Value: string }> }) => response.Claims ?? [],
}))

describe('authSlice', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('login.fulfilled stores token, user, and persists to localStorage', async () => {
    vi.mocked(authApi.loginApi).mockResolvedValue({
      Token: 'jwt-token',
      RefreshToken: 'refresh-token',
      Claims: [
        { Type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email', Value: 'user@test.com' },
        { Type: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role', Value: 'Admin' },
      ],
    })

    const store = configureStore({ reducer: { auth: authReducer } })
    await store.dispatch(login({ userName: 'testuser', password: 'secret', companyDb: 'PBBPL_UAT' }))

    const state = store.getState().auth
    expect(state.isAuthenticated).toBe(true)
    expect(state.token).toBe('jwt-token')
    expect(state.user?.email).toBe('user@test.com')
    expect(state.user?.role).toBe('Admin')
    expect(localStorage.getItem(STORAGE_KEYS.TOKEN)).toBe('jwt-token')
    expect(localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN)).toBe('refresh-token')
  })

  it('login.rejected sets error message', async () => {
    vi.mocked(authApi.loginApi).mockRejectedValue(new Error('Invalid credentials'))

    const store = configureStore({ reducer: { auth: authReducer } })
    await store.dispatch(login({ userName: 'baduser', password: 'x', companyDb: 'PBBPL_UAT' }))

    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(store.getState().auth.error).toBe('Invalid credentials')
  })

  it('logout clears auth state and storage', () => {
    localStorage.setItem(STORAGE_KEYS.TOKEN, 't')
    localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify({ id: '1', email: 'a@b.com', name: 'A', role: 'Standard' }))
    localStorage.setItem(STORAGE_KEYS.BRANCH_ID, '2')

    const store = configureStore({
      reducer: { auth: authReducer },
      preloadedState: {
        auth: {
          user: { id: '1', email: 'a@b.com', name: 'A', role: 'Standard' },
          token: 't',
          companyDb: 'PBBPL_UAT',
          branchId: 2,
          isAuthenticated: true,
          isLoading: false,
          error: null,
        },
      },
    })

    store.dispatch(logout())

    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(store.getState().auth.branchId).toBeNull()
    expect(localStorage.getItem(STORAGE_KEYS.TOKEN)).toBeNull()
    expect(localStorage.getItem(STORAGE_KEYS.BRANCH_ID)).toBeNull()
  })

  it('switchCompany.fulfilled updates company and clears branch', async () => {
    vi.mocked(authApi.switchCompanyApi).mockResolvedValue({
      Token: 'new-token',
      RefreshToken: 'new-refresh',
      Claims: [
        { Type: 'CompanyDb', Value: 'PBBPL_LIVE' },
        { Type: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email', Value: 'user@test.com' },
      ],
    })

    const store = configureStore({
      reducer: { auth: authReducer },
      preloadedState: {
        auth: {
          user: { id: '1', email: 'user@test.com', name: 'User', role: 'Standard' },
          token: 'old-token',
          companyDb: 'PBBPL_UAT',
          branchId: 4,
          isAuthenticated: true,
          isLoading: false,
          error: null,
        },
      },
    })

    await store.dispatch(switchCompany({ companyDb: 'PBBPL_LIVE', password: 'secret' }))

    const state = store.getState().auth
    expect(state.isAuthenticated).toBe(true)
    expect(state.companyDb).toBe('PBBPL_LIVE')
    expect(state.branchId).toBeNull()
    expect(state.token).toBe('new-token')
    expect(localStorage.getItem(STORAGE_KEYS.COMPANY_DB)).toBe('PBBPL_LIVE')
    expect(localStorage.getItem(STORAGE_KEYS.BRANCH_ID)).toBeNull()
  })

  it('switchCompany.rejected keeps session intact', async () => {
    vi.mocked(authApi.switchCompanyApi).mockRejectedValue(new Error('Invalid credentials'))

    const store = configureStore({
      reducer: { auth: authReducer },
      preloadedState: {
        auth: {
          user: { id: '1', email: 'user@test.com', name: 'User', role: 'Standard' },
          token: 'token',
          companyDb: 'PBBPL_UAT',
          branchId: null,
          isAuthenticated: true,
          isLoading: false,
          error: null,
        },
      },
    })

    await store.dispatch(switchCompany({ companyDb: 'PBBPL_LIVE', password: 'wrong' }))

    expect(store.getState().auth.isAuthenticated).toBe(true)
    expect(store.getState().auth.token).toBe('token')
    expect(store.getState().auth.error).toBe('Invalid credentials')
  })
})
