import { describe, expect, it } from 'vitest'
import {
  applyOtherTermsToPo,
  readOtherTermsFromPo,
  resolveLineUoms,
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
