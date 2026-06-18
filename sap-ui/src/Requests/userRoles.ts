import { apiGet, apiPut } from '@/helpers/api/client'

export interface UserWithRoles {
  id: number
  userName?: string
  email?: string
  fullName?: string
  roles: string[]
}

function normalizeUser(raw: Record<string, unknown>): UserWithRoles {
  return {
    id: Number(raw.id ?? raw.Id ?? 0),
    userName: String(raw.userName ?? raw.UserName ?? ''),
    email: String(raw.email ?? raw.Email ?? ''),
    fullName: String(raw.fullName ?? raw.FullName ?? ''),
    roles: (raw.roles ?? raw.Roles ?? []) as string[],
  }
}

export async function getUsersWithRoles() {
  const data = await apiGet<Record<string, unknown>[]>('/user-roles/users')
  return (data ?? []).map(normalizeUser).filter((u) => u.id > 0)
}

export async function getRoles() {
  const data = await apiGet<string[]>('/user-roles/roles')
  return data ?? []
}

export async function updateUserRoles(userId: number, roles: string[]) {
  return apiPut(`/user-roles/users/${userId}/roles`, { roles })
}
