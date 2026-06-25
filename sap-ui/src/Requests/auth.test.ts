import { describe, expect, it, vi, beforeEach } from 'vitest'
import { registerApi } from './auth'
import * as apiClient from '@/helpers/api/client'
import * as rsa from '@/helpers/lib/rsa'

vi.mock('@/helpers/api/client', () => ({
  apiPost: vi.fn(),
}))

vi.mock('@/helpers/lib/rsa', () => ({
  rsaEncrypt: vi.fn(async (value: string) => `encrypted:${value}`),
}))

describe('registerApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(apiClient.apiPost).mockResolvedValue({ success: true, data: { succeeded: true } })
  })

  it('sends onboarding fields with encrypted password', async () => {
    await registerApi({
      fullName: 'Jane Doe',
      userName: 'jdoe',
      email: 'jane@company.com',
      password: 'Secret123!',
    })

    expect(rsa.rsaEncrypt).toHaveBeenCalledWith('Secret123!')
    expect(apiClient.apiPost).toHaveBeenCalledWith('/auth/register', {
      fullName: 'Jane Doe',
      userName: 'jdoe',
      email: 'jane@company.com',
      password: 'encrypted:Secret123!',
    })
  })
})
