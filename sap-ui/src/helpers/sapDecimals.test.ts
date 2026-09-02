import { describe, expect, it } from 'vitest'
import { formatSapDecimal, formatSapFixed, SAP_DECIMAL_PLACES, sapDecimalStep } from './sapDecimals'

describe('sapDecimals', () => {
  it('uses the company decimal-place settings', () => {
    expect(SAP_DECIMAL_PLACES.quantities).toBe(4)
    expect(SAP_DECIMAL_PLACES.amounts).toBe(2)
    expect(SAP_DECIMAL_PLACES.prices).toBe(3)
    expect(SAP_DECIMAL_PLACES.percent).toBe(2)
  })

  it('builds HTML number steps from decimal places', () => {
    expect(sapDecimalStep(4)).toBe('0.0001')
    expect(sapDecimalStep(3)).toBe('0.001')
    expect(sapDecimalStep(2)).toBe('0.01')
    expect(sapDecimalStep(0)).toBe('1')
  })

  it('formats quantities with up to four places and amounts with two', () => {
    expect(formatSapDecimal(1.25, SAP_DECIMAL_PLACES.quantities)).toMatch(/1\.25/)
    expect(formatSapDecimal(1.23456, SAP_DECIMAL_PLACES.quantities)).toMatch(/1\.2346/)
    expect(formatSapFixed(12.3, SAP_DECIMAL_PLACES.amounts)).toMatch(/12\.30/)
  })
})
