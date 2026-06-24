import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROUTES } from '@/config/constants'
import { useAppDispatch } from '@/store/hooks'
import { logout } from '@/store/slices/authSlice'

/** Keeps Redux auth state in sync when axios clears the session after a 401. */
export function AuthSessionListener() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()

  useEffect(() => {
    const handler = () => {
      dispatch(logout())
      navigate(ROUTES.LOGIN, { replace: true })
    }
    window.addEventListener('auth:session-expired', handler)
    return () => window.removeEventListener('auth:session-expired', handler)
  }, [dispatch, navigate])

  return null
}
