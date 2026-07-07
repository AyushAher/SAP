/** Keeps decimal amount inputs non-negative while the user is typing. */
export function sanitizeNonNegativeAmountInput(value: string): string {
  if (value === '') return ''

  let cleaned = value.replace(/-/g, '').replace(/[^\d.]/g, '')
  const [whole = '', ...fractionParts] = cleaned.split('.')
  if (fractionParts.length > 0) {
    cleaned = `${whole}.${fractionParts.join('')}`
  }

  return cleaned
}

export function isNegativeAmountInputKey(key: string): boolean {
  return key === '-' || key === 'e' || key === 'E' || key === '+'
}
