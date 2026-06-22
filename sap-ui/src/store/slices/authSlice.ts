import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit'
import { DEFAULT_COMPANY_DB } from '@/config/companyDb'
import { STORAGE_KEYS } from '@/config/constants'
import {
  getLoginClaims,
  getLoginRefreshToken,
  getLoginToken,
  loginApi,
  logoutApi,
  switchBranchApi,
  switchCompanyApi,
  type AuthClaim,
} from '@/Requests/auth'
import type { LoginCredentials, User } from '@/types'

interface AuthSliceState {
  user: User | null
  token: string | null
  companyDb: string | null
  branchId: number | null
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

  const companyDb = claimValue(claims, 'CompanyDb')
  const branchClaim = claimValue(claims, 'Branch')
  const branchId = branchClaim ? Number(branchClaim) : null

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
    companyDb: companyDb || undefined,
    branchId: Number.isFinite(branchId) ? branchId : null,
  }
}

function persistAuth(
  user: User,
  token: string,
  refreshToken: string | undefined,
  companyDb: string,
  branchId: number | null,
) {
  localStorage.setItem(STORAGE_KEYS.TOKEN, token)
  if (refreshToken) {
    localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, refreshToken)
  }
  localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(user))
  localStorage.setItem(STORAGE_KEYS.COMPANY_DB, companyDb)
  if (branchId === null) {
    localStorage.removeItem(STORAGE_KEYS.BRANCH_ID)
  } else {
    localStorage.setItem(STORAGE_KEYS.BRANCH_ID, String(branchId))
  }
}

function clearStoredAuth() {
  localStorage.removeItem(STORAGE_KEYS.TOKEN)
  localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN)
  localStorage.removeItem(STORAGE_KEYS.USER)
  localStorage.removeItem(STORAGE_KEYS.COMPANY_DB)
  localStorage.removeItem(STORAGE_KEYS.BRANCH_ID)
}

function loadStoredAuth(): Pick<AuthSliceState, 'user' | 'token' | 'companyDb' | 'branchId' | 'isAuthenticated'> {
  const token = localStorage.getItem(STORAGE_KEYS.TOKEN)
  const userRaw = localStorage.getItem(STORAGE_KEYS.USER)
  const companyDb = localStorage.getItem(STORAGE_KEYS.COMPANY_DB)
  const branchRaw = localStorage.getItem(STORAGE_KEYS.BRANCH_ID)
  const branchId = branchRaw ? Number(branchRaw) : null
  if (token && userRaw) {
    try {
      const user = JSON.parse(userRaw) as User
      return {
        user,
        token,
        companyDb: companyDb ?? user.companyDb ?? DEFAULT_COMPANY_DB,
        branchId: Number.isFinite(branchId) ? branchId : user.branchId ?? null,
        isAuthenticated: true,
      }
    } catch {
      clearStoredAuth()
    }
  }
  return { user: null, token: null, companyDb: null, branchId: null, isAuthenticated: false }
}

function applyAuthSession(
  state: AuthSliceState,
  payload: { user: User; token: string; refreshToken?: string; companyDb: string; branchId: number | null },
) {
  state.user = payload.user
  state.token = payload.token
  state.companyDb = payload.companyDb
  state.branchId = payload.branchId
  state.isAuthenticated = true
  persistAuth(payload.user, payload.token, payload.refreshToken, payload.companyDb, payload.branchId)
}

export const login = createAsyncThunk(
  'auth/login',
  async (credentials: LoginCredentials, { rejectWithValue }) => {
    try {
      const response = await loginApi(credentials.userName, credentials.password, credentials.companyDb)
      const token = getLoginToken(response)
      const refreshToken = getLoginRefreshToken(response)
      if (!token) return rejectWithValue('Invalid credentials')
      const user = claimsToUser(getLoginClaims(response), credentials.userName)
      return { user, token, refreshToken, companyDb: credentials.companyDb, branchId: null as number | null }
    } catch (err) {
      return rejectWithValue(err instanceof Error ? err.message : 'Login failed')
    }
  },
)

export const switchCompany = createAsyncThunk(
  'auth/switchCompany',
  async ({ companyDb, password }: { companyDb: string; password: string }, { rejectWithValue }) => {
    try {
      const response = await switchCompanyApi(companyDb as typeof DEFAULT_COMPANY_DB, password)
      const token = getLoginToken(response)
      const refreshToken = getLoginRefreshToken(response)
      if (!token) return rejectWithValue('Unable to switch company database')
      const user = claimsToUser(getLoginClaims(response), '')
      return { user, token, refreshToken, companyDb, branchId: null as number | null }
    } catch (err) {
      return rejectWithValue(err instanceof Error ? err.message : 'Company switch failed')
    }
  },
)

export const switchBranch = createAsyncThunk(
  'auth/switchBranch',
  async (branchId: number | null, { rejectWithValue, getState }) => {
    try {
      const response = await switchBranchApi(branchId)
      const token = getLoginToken(response)
      const refreshToken = getLoginRefreshToken(response)
      if (!token) return rejectWithValue('Unable to switch branch')
      const user = claimsToUser(getLoginClaims(response), '')
      const companyDb = (getState() as { auth: AuthSliceState }).auth.companyDb ?? DEFAULT_COMPANY_DB
      return { user, token, refreshToken, companyDb, branchId }
    } catch (err) {
      return rejectWithValue(err instanceof Error ? err.message : 'Branch switch failed')
    }
  },
)

export const logoutUser = createAsyncThunk('auth/logoutUser', async () => {
  try {
    await logoutApi()
  } catch {
    // Clear local session even if API logout fails
  }
})

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
      state.companyDb = null
      state.branchId = null
      state.isAuthenticated = false
      state.error = null
      clearStoredAuth()
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
        applyAuthSession(state, action.payload)
      })
      .addCase(login.rejected, (state, action) => {
        state.isLoading = false
        state.error = action.payload as string
      })
      .addCase(switchCompany.pending, (state) => {
        state.isLoading = true
        state.error = null
      })
      .addCase(switchCompany.fulfilled, (state, action) => {
        state.isLoading = false
        applyAuthSession(state, action.payload)
      })
      .addCase(switchCompany.rejected, (state, action) => {
        state.isLoading = false
        state.error = action.payload as string
      })
      .addCase(switchBranch.pending, (state) => {
        state.isLoading = true
        state.error = null
      })
      .addCase(switchBranch.fulfilled, (state, action) => {
        state.isLoading = false
        applyAuthSession(state, action.payload)
      })
      .addCase(switchBranch.rejected, (state, action) => {
        state.isLoading = false
        state.error = action.payload as string
      })
      .addCase(logoutUser.fulfilled, (state) => {
        state.user = null
        state.token = null
        state.companyDb = null
        state.branchId = null
        state.isAuthenticated = false
        state.error = null
        clearStoredAuth()
      })
  },
})

export const { logout, clearError, setUser } = authSlice.actions
export default authSlice.reducer
