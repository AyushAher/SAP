import { describe, expect, it } from 'vitest'
import { isNegativeAmountInputKey, sanitizeNonNegativeAmountInput } from './numericInput'

describe('numericInput', () => {
  it('sanitizeNonNegativeAmountInput removes minus signs and invalid characters', () => {
    expect(sanitizeNonNegativeAmountInput('-100')).toBe('100')
    expect(sanitizeNonNegativeAmountInput('12.34')).toBe('12.34')
    expect(sanitizeNonNegativeAmountInput('12.3.4')).toBe('12.34')
    expect(sanitizeNonNegativeAmountInput('abc')).toBe('')
    expect(sanitizeNonNegativeAmountInput('')).toBe('')
    expect(sanitizeNonNegativeAmountInput('1.')).toBe('1.')
    expect(sanitizeNonNegativeAmountInput('1.23456', 4)).toBe('1.2345')
    expect(sanitizeNonNegativeAmountInput('12.349', 2)).toBe('12.34')
  })

  it('isNegativeAmountInputKey blocks scientific notation and sign keys', () => {
    expect(isNegativeAmountInputKey('-')).toBe(true)
    expect(isNegativeAmountInputKey('e')).toBe(true)
    expect(isNegativeAmountInputKey('5')).toBe(false)
  })
})
