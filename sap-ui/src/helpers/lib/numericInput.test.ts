import { describe, expect, it } from 'vitest'
import { isNegativeAmountInputKey, sanitizeNonNegativeAmountInput } from './numericInput'

describe('numericInput', () => {
  it('sanitizeNonNegativeAmountInput removes minus signs and invalid characters', () => {
    expect(sanitizeNonNegativeAmountInput('-100')).toBe('100')
    expect(sanitizeNonNegativeAmountInput('12.34')).toBe('12.34')
    expect(sanitizeNonNegativeAmountInput('12.3.4')).toBe('12.34')
    expect(sanitizeNonNegativeAmountInput('abc')).toBe('')
    expect(sanitizeNonNegativeAmountInput('')).toBe('')
  })

  it('isNegativeAmountInputKey blocks scientific notation and sign keys', () => {
    expect(isNegativeAmountInputKey('-')).toBe(true)
    expect(isNegativeAmountInputKey('e')).toBe(true)
    expect(isNegativeAmountInputKey('5')).toBe(false)
  })
})
