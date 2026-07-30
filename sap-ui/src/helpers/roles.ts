import { ROLES } from '@/config/constants'
import type { User } from '@/types'

/** Claims can arrive as a single role or a role array depending on the token shape. */
export function getUserRoles(user?: User | null): string[] {
  if (!user) return []
  if (user.roles?.length) return user.roles
  return user.role ? [user.role] : []
}

export function isAdminUser(user?: User | null): boolean {
  return getUserRoles(user).some((role) => role === ROLES.SUPER_ADMIN || role === ROLES.ADMIN)
}
