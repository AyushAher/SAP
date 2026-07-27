import { FileQuestion } from 'lucide-react'
import { ROUTES, STORAGE_KEYS } from '@/config/constants'
import { StatusErrorPage } from '@/Pages/errors/StatusErrorPage'

export function NotFoundPage() {
  const isAuthenticated = Boolean(localStorage.getItem(STORAGE_KEYS.TOKEN))

  return (
    <StatusErrorPage
      code="404"
      title="Page not found"
      description="The page you are looking for does not exist, was moved, or the link may be incorrect."
      icon={FileQuestion}
      primaryTo={isAuthenticated ? ROUTES.HOME : ROUTES.LOGIN}
      primaryLabel={isAuthenticated ? 'Back to dashboard' : 'Go to login'}
      secondaryTo={isAuthenticated ? undefined : ROUTES.LOGIN}
      secondaryLabel={undefined}
    />
  )
}
