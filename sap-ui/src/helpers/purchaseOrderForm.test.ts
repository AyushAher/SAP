import { describe, expect, it } from 'vitest'
import { resolveLineUoms } from './purchaseOrderForm'

describe('resolveLineUoms', () => {
  it('defaults both UoMs from the item master when the line has none', () => {
    const result = resolveLineUoms({}, { purchaseUom: 'BOX', stockUom: 'EA' })
    expect(result).toEqual({ purchaseUom: 'BOX', stockUom: 'EA' })
  })

  it('keeps a user-entered purchase UoM', () => {
    const result = resolveLineUoms({ UoMCode: 'PKT' }, { purchaseUom: 'BOX', stockUom: 'EA' })
    expect(result.purchaseUom).toBe('PKT')
  })

  it('always overrides stock UoM with the item master value', () => {
    const result = resolveLineUoms({ StockUom: 'STALE' }, { purchaseUom: 'BOX', stockUom: 'EA' })
    expect(result.stockUom).toBe('EA')
  })

  it('falls back to the existing stock UoM when the master is unknown', () => {
    const result = resolveLineUoms({ StockUom: 'EA' }, undefined)
    expect(result.stockUom).toBe('EA')
  })

  it('returns undefined instead of empty strings', () => {
    expect(resolveLineUoms({}, undefined)).toEqual({ purchaseUom: undefined, stockUom: undefined })
  })

  it('uses UomName as the purchase UoM fallback', () => {
    const result = resolveLineUoms({ UomName: 'KG' }, { purchaseUom: 'BOX', stockUom: 'EA' })
    expect(result.purchaseUom).toBe('KG')
  })
})
