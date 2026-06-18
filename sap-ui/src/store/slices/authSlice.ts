import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit'
import { STORAGE_KEYS } from '@/config/constants'
import { getLoginClaims, getLoginRefreshToken, getLoginToken, loginApi, type AuthClaim } from '@/Requests/auth'
import type { LoginCredentials, User } from '@/types'

interface AuthSliceState {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  error: string | null
}

function claimValue(claims: AuthClaim[], ...types: string[]): string {
  for (const type of types) {
    const match = claims.find((c) => {
      const claimType = c.Type ?? c.type ?? ''
      return claimType === type || claimType.toLowerCase().includes(type.toLowerCase())
    })
    const value = match?.Value ?? match?.value
    if (value) return value
  }
  return ''
}

function claimsToUser(claims: AuthClaim[], fallbackUserName: string): User {
  const roles = claims
    .filter((c) => (c.Type ?? c.type ?? '').toLowerCase().includes('role'))
    .map((c) => c.Value ?? c.value ?? '')
    .filter(Boolean)

  return {
    id: claimValue(
      claims,
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
      'nameidentifier',
    ),
    email: claimValue(
      claims,
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
      'email',
    ) || fallbackUserName,
    name: claimValue(claims, 'FullName', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name') || fallbackUserName,
    role: roles[0] ?? 'Standard',
    roles,
  }
}

function loadStoredAuth(): Pick<AuthSliceState, 'user' | 'token' | 'isAuthenticated'> {
  const token = localStorage.getItem(STORAGE_KEYS.TOKEN)
  const userRaw = localStorage.getItem(STORAGE_KEYS.USER)
  if (token && userRaw) {
    try {
      const user = JSON.parse(userRaw) as User
      return { user, token, isAuthenticated: true }
    } catch {
      localStorage.removeItem(STORAGE_KEYS.TOKEN)
      localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
      localStorage.removeItem(STORAGE_KEYS.USER)
    }
  }
  return { user: null, token: null, isAuthenticated: false }
}

export const login = createAsyncThunk(
  'auth/login',
  async (credentials: LoginCredentials, { rejectWithValue }) => {
    try {
      const response = await loginApi(credentials.userName, credentials.password)
      const token = getLoginToken(response)
      const refreshToken = getLoginRefreshToken(response)
      if (!token) return rejectWithValue('Invalid credentials')
      const user = claimsToUser(getLoginClaims(response), credentials.userName)
      return { user, token, refreshToken }
    } catch (err) {
      return rejectWithValue(err instanceof Error ? err.message : 'Login failed')
    }
  },
)

const stored = loadStoredAuth()

const initialState: AuthSliceState = {
  ...stored,
  isLoading: false,
  error: null,
}

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    logout(state) {
      state.user = null
      state.token = null
      state.isAuthenticated = false
      state.error = null
      localStorage.removeItem(STORAGE_KEYS.TOKEN)
      localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
      localStorage.removeItem(STORAGE_KEYS.USER)
    },
    clearError(state) {
      state.error = null
    },
    setUser(state, action: PayloadAction<User>) {
      state.user = action.payload
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(login.pending, (state) => {
        state.isLoading = true
        state.error = null
      })
      .addCase(login.fulfilled, (state, action) => {
        state.isLoading = false
        state.user = action.payload.user
        state.token = action.payload.token
        state.isAuthenticated = true
        localStorage.setItem(STORAGE_KEYS.TOKEN, action.payload.token)
        if (action.payload.refreshToken) {
          localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, action.payload.refreshToken)
        }
        localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(action.payload.user))
      })
      .addCase(login.rejected, (state, action) => {
        state.isLoading = false
        state.error = action.payload as string
      })
  },
})

export const { logout, clearError, setUser } = authSlice.actions
export default authSlice.reducer
