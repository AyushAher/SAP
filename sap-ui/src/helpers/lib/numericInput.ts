/** Keeps decimal amount inputs non-negative while the user is typing. */
export function sanitizeNonNegativeAmountInput(value: string, maxFractionDigits?: number): string {
  if (value === '') return ''

  let cleaned = value.replace(/-/g, '').replace(/[^\d.]/g, '')
  const [whole = '', ...fractionParts] = cleaned.split('.')
  if (fractionParts.length > 0) {
    let fraction = fractionParts.join('')
    if (maxFractionDigits != null && Number.isFinite(maxFractionDigits) && maxFractionDigits >= 0) {
      fraction = fraction.slice(0, maxFractionDigits)
    }
    cleaned = `${whole}.${fraction}`
  }

  return cleaned
}

export function isNegativeAmountInputKey(key: string): boolean {
  return key === '-' || key === 'e' || key === 'E' || key === '+'
}
