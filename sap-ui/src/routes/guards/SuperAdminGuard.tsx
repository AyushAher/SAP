import { Navigate } from 'react-router-dom'
import { useAppSelector } from '@/store/hooks'
import { ROUTES } from '@/config/constants'
import { PageSpinner } from '@/Components/ui'
import { isSuperAdminUser } from '@/helpers/roles'

interface SuperAdminGuardProps {
  children: React.ReactNode
}

export function SuperAdminGuard({ children }: SuperAdminGuardProps) {
  const { isAuthenticated, isLoading, user } = useAppSelector((state) => state.auth)

  if (isLoading) {
    return <PageSpinner />
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} replace />
  }

  if (!isSuperAdminUser(user)) {
    return <Navigate to={ROUTES.FORBIDDEN} replace />
  }

  return <>{children}</>
}
