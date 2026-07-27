import { useEffect } from 'react'
import { Outlet, useNavigate } from 'react-router-dom'
import { ROUTES } from '@/config/constants'
import { useAppDispatch } from '@/store/hooks'
import { logout } from '@/store/slices/authSlice'

/**
 * Mounted for the whole app so 401/403 from axios can navigate even outside MainLayout.
 */
export function RootLayout() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()

  useEffect(() => {
    const onSessionExpired = () => {
      dispatch(logout())
      if (!window.location.pathname.startsWith('/auth')) {
        navigate(ROUTES.LOGIN, {
          replace: true,
          state: { from: { pathname: window.location.pathname } },
        })
      }
    }

    const onForbidden = () => {
      if (window.location.pathname === ROUTES.FORBIDDEN) return
      navigate(ROUTES.FORBIDDEN, { replace: true })
    }

    window.addEventListener('auth:session-expired', onSessionExpired)
    window.addEventListener('app:forbidden', onForbidden)
    return () => {
      window.removeEventListener('auth:session-expired', onSessionExpired)
      window.removeEventListener('app:forbidden', onForbidden)
    }
  }, [dispatch, navigate])

  return <Outlet />
}
