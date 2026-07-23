import { apiGet, apiPost, apiPut, apiPatch, apiDelete } from '@/helpers/api/client'

export interface UserGroupMember {
  userId: number
  userName?: string
  fullName?: string
  email?: string
}

export interface UserGroup {
  id: number
  name: string
  description?: string
  isActive: boolean
  createdAt: string
  members: UserGroupMember[]
}

export interface UpsertUserGroupPayload {
  name: string
  description?: string
  memberUserIds: number[]
}

export async function getUserGroups() {
  return apiGet<UserGroup[]>('/user-groups')
}

export async function createUserGroup(payload: UpsertUserGroupPayload) {
  return apiPost('/user-groups', payload)
}

export async function updateUserGroup(id: number, payload: UpsertUserGroupPayload) {
  return apiPut(`/user-groups/${id}`, payload)
}

export async function setUserGroupActive(id: number, isActive: boolean) {
  return apiPatch(`/user-groups/${id}/active`, { isActive })
}

export async function deleteUserGroup(id: number) {
  return apiDelete(`/user-groups/${id}`)
}
