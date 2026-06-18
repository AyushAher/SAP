import '@testing-library/jest-dom/vitest'
import { vi } from 'vitest'

vi.mock('@/helpers/lib/rsa', () => ({
  rsaEncrypt: (value: string) => value,
}))

const storage = new Map<string, string>()

Object.defineProperty(window, 'localStorage', {
  value: {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => { storage.set(key, value) },
    removeItem: (key: string) => { storage.delete(key) },
    clear: () => { storage.clear() },
    get length() { return storage.size },
    key: (index: number) => [...storage.keys()][index] ?? null,
  },
  writable: true,
})

beforeEach(() => {
  storage.clear()
})
