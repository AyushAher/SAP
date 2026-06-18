import { describe, expect, it, beforeEach, vi } from 'vitest'
import { configureStore } from '@reduxjs/toolkit'
import authReducer, { login, logout } from './authSlice'
import { STORAGE_KEYS } from '@/config/constants'
import * as authApi from '@/Requests/auth'

vi.mock('@/Requests/auth', () => ({
  loginApi: vi.fn(),
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
    await store.dispatch(login({ userName: 'testuser', password: 'secret' }))

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
    await store.dispatch(login({ userName: 'baduser', password: 'x' }))

    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(store.getState().auth.error).toBe('Invalid credentials')
  })

  it('logout clears auth state and storage', () => {
    localStorage.setItem(STORAGE_KEYS.TOKEN, 't')
    localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify({ id: '1', email: 'a@b.com', name: 'A', role: 'Standard' }))

    const store = configureStore({
      reducer: { auth: authReducer },
      preloadedState: {
        auth: {
          user: { id: '1', email: 'a@b.com', name: 'A', role: 'Standard' },
          token: 't',
          isAuthenticated: true,
          isLoading: false,
          error: null,
        },
      },
    })

    store.dispatch(logout())

    expect(store.getState().auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem(STORAGE_KEYS.TOKEN)).toBeNull()
  })
})
