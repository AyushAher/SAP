import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { Provider } from 'react-redux'
import { configureStore } from '@reduxjs/toolkit'
import authReducer from '@/store/slices/authSlice'
import { AuthGuard } from './AuthGuard'
import { ROUTES } from '@/config/constants'

function renderGuard(isAuthenticated: boolean, isLoading = false) {
  const store = configureStore({
    reducer: { auth: authReducer },
    preloadedState: {
      auth: {
        user: isAuthenticated ? { id: '1', email: 'u@test.com', name: 'User', role: 'Standard' } : null,
        token: isAuthenticated ? 'token' : null,
        isAuthenticated,
        isLoading,
        error: null,
      },
    },
  })

  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={['/protected']}>
        <Routes>
          <Route
            path="/protected"
            element={
              <AuthGuard>
                <div>Protected content</div>
              </AuthGuard>
            }
          />
          <Route path={ROUTES.LOGIN} element={<div>Login page</div>} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  )
}

describe('AuthGuard', () => {
  it('renders children when authenticated', () => {
    renderGuard(true)
    expect(screen.getByText('Protected content')).toBeInTheDocument()
  })

  it('redirects unauthenticated users to login', () => {
    renderGuard(false)
    expect(screen.getByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })

  it('shows spinner while auth state is loading', () => {
    renderGuard(false, true)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})
