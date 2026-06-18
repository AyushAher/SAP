import { describe, expect, it } from 'vitest'
import {
  getPayableAmount,
  isPaidRecord,
  isPaymentTermSelectable,
  normalizeStatus,
  paymentTermLabel,
  resolveDisplayPayable,
} from './stageWisePaymentCalculations'

const po = { DocTotal: 1000, VatSum: 180, DocumentStatus: 'bost_Open' as const }
const paymentTerms = [
  { id: 1, desc: 'Advance', basic: 30, gst: 30, type: 'Advance' },
  { id: 2, desc: 'Balance', basic: 70, gst: 70, type: 'Balance' },
]

describe('stageWisePaymentCalculations', () => {
  it('paymentTermLabel prefers description over percentages', () => {
    expect(paymentTermLabel({ desc: 'Milestone 1', basic: 10, gst: 5 })).toBe('Milestone 1')
    expect(paymentTermLabel({ basic: 10, gst: 5 })).toBe('Basic 10% & GST 5%')
  })

  it('isPaymentTermSelectable requires desc or percentages', () => {
    expect(isPaymentTermSelectable({ desc: 'X' })).toBe(true)
    expect(isPaymentTermSelectable({ basic: 10 })).toBe(true)
    expect(isPaymentTermSelectable({})).toBe(false)
  })

  it('normalizeStatus maps numeric and string statuses', () => {
    expect(normalizeStatus(0)).toBe('Created')
    expect(normalizeStatus('1')).toBe('Approval Pending')
    expect(normalizeStatus('Approved')).toBe('Approved')
  })

  it('isPaidRecord detects paid down-payment records', () => {
    expect(isPaidRecord({
      id: 1,
      status: 2,
      apDownPaymentInvoiceEntryNumber: '123',
    })).toBe(true)
    expect(isPaidRecord({ id: 1, status: 1 })).toBe(false)
  })

  it('getPayableAmount subtracts already requested amounts for a term', () => {
    const activeRecords = [
      { id: 1, paymentTermsType: 1, grossAmount: 100, gstAmount: 18, status: 1 },
    ]
    const payable = getPayableAmount(po, paymentTerms, activeRecords, paymentTerms[0], 1, 1000)
    // 30% of 1000 basic + 30% of 180 VAT - already paid (118) = 236
    expect(payable).toBe(236)
  })

  it('resolveDisplayPayable uses AP invoice balance for invoice terms', () => {
    const closedPo = { ...po, DocumentStatus: 'bost_Close' as const }
    const invoiceTerm = { id: 3, type: 'Invoice', desc: 'Invoice' }
    const payable = resolveDisplayPayable(
      closedPo,
      [invoiceTerm],
      [],
      invoiceTerm,
      '99',
      { DocTotal: 500, PaidToDate: 100, WTAmount: 10 },
      1000,
    )
    expect(payable).toBe(410)
  })
})
