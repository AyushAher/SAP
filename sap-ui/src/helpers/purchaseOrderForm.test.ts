import { describe, expect, it } from 'vitest'
import {
  applyLogisticsToPo,
  applyOtherTermsToPo,
  applyPaymentPercentToTerm,
  applyPaymentTermsToPo,
  isGstPaymentTermType,
  normalizePaymentTermType,
  parsePaymentTermsFromPo,
  readLogisticsFromPo,
  readOtherTermsFromPo,
  resolveLineUoms,
  resolvePaymentTermPercent,
} from './purchaseOrderForm'

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

describe('other terms OPOR UDFs', () => {
  it('writes Service Layer field names used on OPOR', () => {
    const payload = applyOtherTermsToPo({}, {
      deliveryTerms: 'FOB',
      inspectionBy: 'QC',
      transportation: 'Road',
      supervision: 'Site',
      transitInsurance: 'Vendor',
      drawingDocuments: 'GA',
      loading: 'Vendor',
      warranty: '12m',
      unloading: 'Buyer',
      otherRemark: 'Careful',
      painting: 'Epoxy',
      testCertificates: 'MTC',
    })

    expect(payload).toMatchObject({
      U_DL: 'FOB',
      U_INSPBY: 'QC',
      U_TRANS: 'Road',
      U_SUPR: 'Site',
      U_TRANINSU: 'Vendor',
      U_DRA_DOC: 'GA',
      U_LOAD: 'Vendor',
      U_WARR: '12m',
      U_UN_LOAD: 'Buyer',
      U_ANOTHREM: 'Careful',
      U_PAIN: 'Epoxy',
      U_TC: 'MTC',
    })
    expect(payload).not.toHaveProperty('U_DelTerms')
    expect(payload).not.toHaveProperty('U_Warranty')
  })

  it('reads real SAP names and falls back to legacy keys', () => {
    expect(readOtherTermsFromPo({ U_DL: 'Net 30', U_WARR: '24m' })).toEqual(
      expect.objectContaining({ deliveryTerms: 'Net 30', warranty: '24m' }),
    )
    expect(readOtherTermsFromPo({ U_DelTerms: 'legacy', U_Warranty: 'old' })).toEqual(
      expect.objectContaining({ deliveryTerms: 'legacy', warranty: 'old' }),
    )
  })

  it('strips legacy invented names when applying terms', () => {
    const payload = applyOtherTermsToPo(
      { U_DelTerms: 'stale', U_WARR: 'keep-me-overwritten' },
      { deliveryTerms: 'fresh', warranty: 'new' },
    )
    expect(payload.U_DL).toBe('fresh')
    expect(payload.U_WARR).toBe('new')
    expect(payload).not.toHaveProperty('U_DelTerms')
  })
})

describe('payment term type → basic/gst mapping', () => {
  it('maps Running to Proforma', () => {
    expect(normalizePaymentTermType('Running')).toBe('Proforma')
    expect(normalizePaymentTermType('running')).toBe('Proforma')
  })

  it('treats GstProforma and TaxInvoice as GST types', () => {
    expect(isGstPaymentTermType('GstProforma')).toBe(true)
    expect(isGstPaymentTermType('TaxInvoice')).toBe(true)
    expect(isGstPaymentTermType('Advance')).toBe(false)
    expect(isGstPaymentTermType('Proforma')).toBe(false)
  })

  it('writes Payment% to U_G for GST types and U_B otherwise', () => {
    expect(applyPaymentPercentToTerm({ type: 'Advance' }, 30)).toEqual({
      type: 'Advance',
      basic: 30,
      gst: undefined,
    })
    expect(applyPaymentPercentToTerm({ type: 'GstProforma' }, 18)).toEqual({
      type: 'GstProforma',
      basic: undefined,
      gst: 18,
    })
  })

  it('resolves Payment% preferring gst for GST types', () => {
    expect(resolvePaymentTermPercent({ type: 'GstProforma', basic: 10, gst: 18 })).toBe(18)
    expect(resolvePaymentTermPercent({ type: 'Advance', basic: 30, gst: 5 })).toBe(30)
    expect(resolvePaymentTermPercent({ type: 'Advance', basic: 0, gst: 12 })).toBe(12)
  })

  it('parses legacy Running and applies mapped fields to PO', () => {
    const terms = parsePaymentTermsFromPo({ U_T1: 'Running', U_B1: 25 })
    expect(terms[0]?.type).toBe('Proforma')
    expect(resolvePaymentTermPercent(terms[0]!)).toBe(25)

    const payload = applyPaymentTermsToPo({}, [
      { id: 1, type: 'TaxInvoice', gst: 18 },
      { id: 2, type: 'Advance', basic: 40 },
    ])
    expect(payload).toMatchObject({
      U_T1: 'TaxInvoice',
      U_G1: 18,
      U_B1: 0,
      U_T2: 'Advance',
      U_B2: 40,
      U_G2: 0,
    })
  })
})

describe('logistics SAP field mapping', () => {
  it('maps to U_CardCode / U_DispachAdd / U_ContactPerson (not ShipToCode)', () => {
    const payload = applyLogisticsToPo({}, {
      dispatchTo: 'C000001',
      dispatchAddress: 'Pune plant',
      contactPerson: 'Ravi',
    })
    expect(payload.U_CardCode).toBe('C000001')
    expect(payload.U_DispachAdd).toBe('Pune plant')
    expect(payload.U_ContactPerson).toBe('Ravi')
    expect(payload.U_DispatchTo).toBeUndefined()
    expect(payload.ShipToCode).toBeUndefined()

    const read = readLogisticsFromPo({
      U_CardCode: 'C000001',
      U_DispachAdd: 'Pune plant',
      U_ContactPerson: 'Ravi',
      ShipToCode: 'SHOULD-NOT-USE',
    })
    expect(read.dispatchTo).toBe('C000001')
    expect(read.dispatchAddress).toBe('Pune plant')
    expect(read.contactPerson).toBe('Ravi')
  })
})
