import JSEncrypt from 'jsencrypt'
import publicKeyPem from '@/config/keys/public.pem?raw'

/**
 * Encrypts plaintext using the RSA public key (PKCS#1 v1.5).
 * Use this before sending sensitive data to the server.
 */
export function rsaEncrypt(plaintext: string): string {
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
export function rsaEncryptObject<T extends Record<string, unknown>>(data: T): string {
  return rsaEncrypt(JSON.stringify(data))
}

export function getPublicKey(): string {
  return publicKeyPem
}
