import { describe, expect, it } from 'vitest'
import { calculatePurchaseRequestTotals, normalizePurchaseRequestHeader } from '@/helpers/purchaseRequestForm'

describe('purchaseRequestForm', () => {
  it('normalizes header dates and requester fields', () => {
    const header = normalizePurchaseRequestHeader({
      DocEntry: 12,
      CardCode: 'V1',
      Requester: 'manager',
      RequriedDate: '2026-09-10',
      DocDueDate: '2026-09-10',
    })
    expect(header.CardCode).toBe('V1')
    expect(header.DocDueDate).toBeTruthy()
  })

  it('totals item lines the same way as purchase orders', () => {
    const totals = calculatePurchaseRequestTotals([
      { Quantity: 2, UnitPrice: 10, DiscountPercent: 0, TaxPercentagePerRow: 18 },
    ], 0)
    expect(totals.totalBeforeDiscount).toBeGreaterThan(0)
  })
})
