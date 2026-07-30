import { describe, expect, it } from 'vitest'
import { getUserRoles, isAdminUser } from './roles'
import type { User } from '@/types'

function user(overrides: Partial<User>): User {
  return { id: '1', email: 'a@b.com', name: 'Tester', role: '', ...overrides }
}

describe('getUserRoles', () => {
  it('returns an empty list when there is no user', () => {
    expect(getUserRoles(null)).toEqual([])
  })

  it('prefers the roles array when present', () => {
    expect(getUserRoles(user({ role: 'Standard', roles: ['Admin', 'Standard'] }))).toEqual(['Admin', 'Standard'])
  })

  it('falls back to the single role claim', () => {
    expect(getUserRoles(user({ role: 'Standard' }))).toEqual(['Standard'])
  })
})

describe('isAdminUser', () => {
  it('allows Admin', () => {
    expect(isAdminUser(user({ role: 'Admin' }))).toBe(true)
  })

  it('allows SuperAdmin', () => {
    expect(isAdminUser(user({ role: 'SuperAdmin' }))).toBe(true)
  })

  it('allows a user holding Admin among several roles', () => {
    expect(isAdminUser(user({ role: 'Standard', roles: ['Standard', 'Admin'] }))).toBe(true)
  })

  it('rejects standard users', () => {
    expect(isAdminUser(user({ role: 'Standard' }))).toBe(false)
  })

  it('rejects anonymous users', () => {
    expect(isAdminUser(undefined)).toBe(false)
  })
})
