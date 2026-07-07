import { describe, expect, it } from 'vitest'
import {
  applySequentialBatchRowAdjustments,
  batchRowRequiresApInvoice,
  getPayableAmount,
  isPaidRecord,
  isPaymentTermSelectable,
  normalizeStatus,
  paymentTermLabel,
  requiresBatchPayment,
  resolveBatchRowPayable,
  resolveDisplayPayable,
  validateBatchPaymentAmounts,
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

  it('resolveDisplayPayable uses AP invoice balance for invoice terms on open PO when AP invoice selected', () => {
    const openPo = { DocTotal: 118, VatSum: 18, DocumentStatus: 'bost_Open' as const }
    const invoiceTerm = { id: 11, type: 'Invoice', gst: 100, basic: null as unknown as number | undefined }
    const payable = resolveDisplayPayable(
      openPo,
      [invoiceTerm],
      [],
      invoiceTerm,
      '99',
      { DocTotal: 500, PaidToDate: 100, WTAmount: 10 },
      100,
    )
    expect(payable).toBe(410)
  })

  it('resolveDisplayPayable returns zero for invoice terms on open PO without AP invoice', () => {
    const openPo = { DocTotal: 118, VatSum: 18, DocumentStatus: 'bost_Open' as const }
    const invoiceTerm = { id: 11, type: 'Invoice', gst: 100, basic: null as unknown as number | undefined }
    const payable = resolveDisplayPayable(
      openPo,
      [invoiceTerm],
      [],
      invoiceTerm,
      '',
      undefined,
      100,
    )
    expect(payable).toBe(18)
  })

  it('resolveBatchRowPayable uses GST percentage for invoice term on open PO without AP invoice', () => {
    const openPo = { DocTotal: 1180, VatSum: 180, DocumentStatus: 'bost_Open' as const }
    const invoiceTerm = { id: 11, type: 'Invoice', gst: 100, basic: 0 }
    const result = resolveBatchRowPayable(
      openPo,
      [invoiceTerm],
      [],
      [11],
      undefined,
      '',
      1000,
    )
    expect(result.balanceDue).toBe(0)
    expect(result.payable).toBe(180)
  })

  it('requiresBatchPayment for closed PO, invoice terms, or AP invoice doc entry', () => {
    const openPo = { DocumentStatus: 'bost_Open' as const }
    const closedPo = { DocumentStatus: 'bost_Close' as const }
    const advanceTerm = { id: 1, type: 'Advance' }
    const invoiceTerm = { id: 2, type: 'Invoice' }

    expect(requiresBatchPayment(openPo, advanceTerm)).toBe(false)
    expect(requiresBatchPayment(openPo, invoiceTerm)).toBe(true)
    expect(requiresBatchPayment(closedPo, advanceTerm)).toBe(true)
    expect(requiresBatchPayment(openPo, advanceTerm, '123')).toBe(true)
  })

  it('resolveBatchRowPayable uses AP invoice balance for closed PO rows', () => {
    const closedPo = { DocTotal: 1000, VatSum: 180, DocumentStatus: 'bost_Close' as const }
    const invoiceTerm = { id: 3, type: 'Invoice', desc: 'Invoice' }
    const result = resolveBatchRowPayable(
      closedPo,
      [invoiceTerm],
      [],
      [3],
      { DocTotal: 500, PaidToDate: 100, WTAmount: 10 },
      '99',
      820,
    )
    expect(result.balanceDue).toBe(410)
    expect(result.payable).toBe(410)
  })

  it('resolveBatchRowPayable uses term percentages for advance rows without AP invoice', () => {
    const openPo = { DocTotal: 1180, VatSum: 180, DocumentStatus: 'bost_Open' as const }
    const advanceTerm = { id: 1, desc: 'Advance', basic: 30, gst: 30, type: 'Advance' }
    const result = resolveBatchRowPayable(
      openPo,
      [advanceTerm],
      [],
      [1],
      undefined,
      '',
      1000,
    )
    expect(result.balanceDue).toBe(0)
    expect(result.payable).toBe(354)
  })

  it('batchRowRequiresApInvoice is false for advance terms on open PO', () => {
    const openPo = { DocumentStatus: 'bost_Open' as const }
    const advanceTerm = { id: 1, type: 'Advance' }
    expect(batchRowRequiresApInvoice(openPo, [advanceTerm], [1])).toBe(false)
    expect(batchRowRequiresApInvoice(openPo, [{ id: 2, type: 'Invoice' }], [2])).toBe(true)
    expect(batchRowRequiresApInvoice({ DocumentStatus: 'bost_Close' }, [advanceTerm], [1])).toBe(true)
  })

  it('applySequentialBatchRowAdjustments preserves payable for rows without AP invoice', () => {
    const context = {
      po: { DocTotal: 1180, VatSum: 180, DocumentStatus: 'bost_Open' as const },
      paymentTerms: [{ id: 1, desc: 'Advance', basic: 30, gst: 30, type: 'Advance' }],
      activeRecords: [],
      totalBasic: 1000,
    }
    const rows = [
      {
        apInvoiceDocEntry: '',
        paymentTermsTypes: ['1'],
        amount: '100',
        baseBalanceDue: 0,
        basePayable: 354,
        balanceDue: 0,
        payable: 354,
      },
      {
        apInvoiceDocEntry: '',
        paymentTermsTypes: ['1'],
        amount: '50',
        baseBalanceDue: 0,
        basePayable: 354,
        balanceDue: 0,
        payable: 354,
      },
    ]
    const adjusted = applySequentialBatchRowAdjustments(rows, context)
    expect(adjusted[0].payable).toBe(354)
    expect(adjusted[1].payable).toBe(254)
  })

  it('validateBatchPaymentAmounts rejects stage totals exceeding payable', () => {
    const context = {
      po: { DocTotal: 1180, VatSum: 180, DocumentStatus: 'bost_Open' as const },
      paymentTerms: [{ id: 1, desc: 'Advance', basic: 30, gst: 30, type: 'Advance' }],
      activeRecords: [],
      totalBasic: 1000,
    }
    const rows = [
      {
        paymentTermsTypes: ['1'],
        amount: '200',
        basePayable: 354,
        payable: 354,
      },
      {
        paymentTermsTypes: ['1'],
        amount: '200',
        basePayable: 354,
        payable: 354,
      },
    ]
    expect(validateBatchPaymentAmounts(rows, context)).toMatch(/cannot exceed payable/i)
  })

  it('applySequentialBatchRowAdjustments deducts prior row amounts for the same AP invoice', () => {
    const rows = [
      {
        apInvoiceDocEntry: '5504',
        amount: '2000',
        baseBalanceDue: 6800,
        basePayable: 6800,
        balanceDue: 6800,
        payable: 6800,
      },
      {
        apInvoiceDocEntry: '5504',
        amount: '1500',
        baseBalanceDue: 6800,
        basePayable: 6800,
        balanceDue: 6800,
        payable: 6800,
      },
      {
        apInvoiceDocEntry: '5505',
        amount: '500',
        baseBalanceDue: 3000,
        basePayable: 3000,
        balanceDue: 3000,
        payable: 3000,
      },
    ]

    const adjusted = applySequentialBatchRowAdjustments(rows)
    expect(adjusted[0].balanceDue).toBe(6800)
    expect(adjusted[1].balanceDue).toBe(4800)
    expect(adjusted[1].payable).toBe(4800)
    expect(adjusted[2].balanceDue).toBe(3000)
  })
})
