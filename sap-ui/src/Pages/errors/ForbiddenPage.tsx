import { useMemo } from 'react'
import { ShieldOff } from 'lucide-react'
import { ROUTES, STORAGE_KEYS } from '@/config/constants'
import {
  clearForbiddenMessage,
  readForbiddenMessage,
} from '@/helpers/forbiddenMessage'
import { StatusErrorPage } from '@/Pages/errors/StatusErrorPage'

export function ForbiddenPage() {
  const description = useMemo(() => {
    const stored = readForbiddenMessage()
    clearForbiddenMessage()
    return (
      stored
      || 'You do not have permission to access this resource. Contact your administrator if you believe this is a mistake.'
    )
  }, [])

  const isAuthenticated = Boolean(localStorage.getItem(STORAGE_KEYS.TOKEN))

  return (
    <StatusErrorPage
      code="403"
      title="Access denied"
      description={description}
      icon={ShieldOff}
      primaryTo={isAuthenticated ? ROUTES.HOME : ROUTES.LOGIN}
      primaryLabel={isAuthenticated ? 'Back to dashboard' : 'Go to login'}
    />
  )
}
