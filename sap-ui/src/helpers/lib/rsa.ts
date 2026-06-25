import JSEncrypt from 'jsencrypt'
import { API_BASE_URL } from '@/config/constants'
import publicKeyPemFallback from '@/config/keys/public.pem?raw'

let cachedPublicKey: string | null = null
let fetchPromise: Promise<string> | null = null

interface PublicKeyApiResponse {
  success?: boolean
  data?: { publicKey?: string }
}

async function resolvePublicKey(): Promise<string> {
  if (cachedPublicKey) return cachedPublicKey

  fetchPromise ??= (async () => {
    const response = await fetch(`${API_BASE_URL}/auth/public-key`)
    if (!response.ok) {
      throw new Error('Failed to fetch encryption public key')
    }

    const body = (await response.json()) as PublicKeyApiResponse
    const key = body.data?.publicKey?.trim()
    if (!key) {
      if (publicKeyPemFallback.trim()) {
        return publicKeyPemFallback
      }
      throw new Error('Server did not return a public key')
    }

    cachedPublicKey = key
    return key
  })().catch((error) => {
    fetchPromise = null
    throw error
  })

  return fetchPromise
}

/**
 * Encrypts plaintext using the server's RSA public key (PKCS#1 v1.5).
 * The key is fetched from /auth/public-key so deployed environments always
 * use the key that matches the API's private key.
 */
export async function rsaEncrypt(plaintext: string): Promise<string> {
  const publicKeyPem = await resolvePublicKey()
  const encryptor = new JSEncrypt()
  encryptor.setPublicKey(publicKeyPem)
  const encrypted = encryptor.encrypt(plaintext)
  if (!encrypted) {
    throw new Error('RSA encryption failed')
  }
  return encrypted
}

/**
 * Encrypts a JSON-serializable object by stringifying and encrypting.
 */
export async function rsaEncryptObject<T extends Record<string, unknown>>(data: T): Promise<string> {
  return rsaEncrypt(JSON.stringify(data))
}

export function clearCachedPublicKey(): void {
  cachedPublicKey = null
  fetchPromise = null
}

export async function getPublicKey(): Promise<string> {
  return resolvePublicKey()
}
