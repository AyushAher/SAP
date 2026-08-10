import { describe, expect, it } from 'vitest'
import {
  applyLogisticsToPo,
  applyOtherTermsToPo,
  applyPaymentPercentToTerm,
  applyPaymentTermsToPo,
  buildPaymentTermDescription,
  dispatchLocationForWarehouse,
  hasGstPaymentTerm,
  isGstPaymentTermType,
  nextPaymentTermSlot,
  normalizePaymentTermType,
  parsePaymentTermsFromPo,
  readLogisticsFromPo,
  readOtherTermsFromPo,
  resolveLineUoms,
  resolvePaymentTermPercent,
  usesPbbplDispatchLocationMapping,
  warehouseForDispatchLocation,
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

  it('writes GST Payment% only to U_G11 and basic terms to U_Bn', () => {
    const terms = parsePaymentTermsFromPo({ U_T1: 'Running', U_B1: 25 })
    expect(terms[0]?.type).toBe('Proforma')
    expect(resolvePaymentTermPercent(terms[0]!)).toBe(25)

    const payload = applyPaymentTermsToPo({}, [
      { id: 1, type: 'TaxInvoice', gst: 18 },
      { id: 2, type: 'Advance', basic: 40 },
    ])
    expect(payload).toMatchObject({
      U_T2: 'Advance',
      U_B2: 40,
      U_G2: 0,
      U_T11: 'TaxInvoice',
      U_G11: 18,
    })
    expect(payload.U_G1).toBe(0)
    expect(payload.U_B1).toBeUndefined()
    expect(payload.U_B11).toBeUndefined()
  })

  it('allows only one GST term on slot 11', () => {
    expect(nextPaymentTermSlot([], 'GstProforma')).toBe(11)
    expect(nextPaymentTermSlot([{ id: 11, type: 'TaxInvoice', gst: 18 }], 'GstProforma')).toBeNull()
    expect(hasGstPaymentTerm([{ id: 11, type: 'GstProforma', gst: 100 }])).toBe(true)
    expect(nextPaymentTermSlot([{ id: 11, type: 'GstProforma', gst: 100 }], 'Advance')).toBe(1)
  })

  it('coalesces legacy GST rows from slot 1–10 into slot 11 when reading', () => {
    const terms = parsePaymentTermsFromPo({
      U_T1: 'Advance',
      U_B1: 80,
      U_T3: 'GstProforma',
      U_G3: 18,
    })
    expect(terms).toEqual([
      { id: 1, type: 'Advance', basic: 80, gst: undefined, stage: undefined, desc: undefined },
      { id: 11, type: 'GstProforma', basic: undefined, gst: 18, stage: undefined, desc: undefined },
    ])
  })

  it('coalesces Invoice + U_G3 into U_G11 and never writes U_G3 on apply', () => {
    const terms = parsePaymentTermsFromPo({
      U_T1: 'Advance',
      U_B1: 20,
      U_T2: 'Invoice',
      U_B2: 80,
      U_T3: 'Invoice',
      U_G3: 100,
    })
    expect(terms).toEqual([
      { id: 1, type: 'Advance', basic: 20, gst: undefined, stage: undefined, desc: undefined },
      { id: 2, type: 'Invoice', basic: 80, gst: undefined, stage: undefined, desc: undefined },
      { id: 11, type: 'Invoice', basic: undefined, gst: 100, stage: undefined, desc: undefined },
    ])

    const payload = applyPaymentTermsToPo({}, terms)
    expect(payload.U_G3).toBe(0)
    expect(payload.U_G11).toBe(100)
    expect(payload.U_T11).toBe('Invoice')
    expect(payload.U_B1).toBe(20)
    expect(payload.U_B2).toBe(80)
    expect(payload.U_T3).toBeUndefined()
  })

  it('routes Amount basis=GST with Invoice type to slot 11', () => {
    expect(nextPaymentTermSlot([], 'Invoice', 'gst')).toBe(11)
    expect(nextPaymentTermSlot([], 'Invoice', 'basic')).toBe(1)
    const payload = applyPaymentTermsToPo({}, [
      { id: 11, type: 'Invoice', gst: 100 },
      { id: 1, type: 'Advance', basic: 20 },
    ])
    expect(payload.U_G11).toBe(100)
    expect(payload.U_G3).toBe(0)
    expect(payload.U_T11).toBe('Invoice')
  })

  it('sends payment term description as %Value Basic|GST Type Stage', () => {
    expect(buildPaymentTermDescription({ id: 1, type: 'Advance', basic: 20, stage: 'Stage1' }))
      .toBe('%20 Basic Advance Stage1')
    expect(buildPaymentTermDescription({ id: 11, type: 'Invoice', gst: 100 }))
      .toBe('%100 GST Invoice')

    const payload = applyPaymentTermsToPo({}, [
      { id: 1, type: 'Advance', basic: 20, stage: 'Stage1' },
      { id: 11, type: 'TaxInvoice', gst: 18 },
    ])
    expect(payload.U_D1).toBe('%20 Basic Advance Stage1')
    expect(payload.U_D11).toBe('%18 GST TaxInvoice')
  })
})

describe('logistics SAP field mapping', () => {
  it('maps to U_CardCode / U_DisID / U_DispachAdd / U_SHIPTO (not ShipToCode)', () => {
    const payload = applyLogisticsToPo({}, {
      dispatchTo: 'C000001',
      dispatchId: 'DISP-99',
      dispatchAddress: 'Pune plant',
      contactPerson: 'Ravi Kumar (9876543210)',
    })
    expect(payload.U_CardCode).toBe('C000001')
    expect(payload.U_DisID).toBe('DISP-99')
    expect(payload.U_DispachAdd).toBe('Pune plant')
    expect(payload.U_SHIPTO).toBe('Ravi Kumar (9876543210)')
    expect(payload.U_ContactPerson).toBeUndefined()
    expect(payload.U_DispatchTo).toBeUndefined()
    expect(payload.ShipToCode).toBeUndefined()

    const withLogistics = applyLogisticsToPo({}, {
      dispatchTo: 'C000001',
      priceBasis: 'F.O.R.',
      modeOfTransport: '1',
    })
    expect(withLogistics.U_PRI_BAS).toBe('F.O.R.')
    expect(withLogistics.U_TransMode).toBe('1')
    expect(withLogistics.U_PriceBasis).toBeUndefined()
    expect(withLogistics.U_ModeOfTransport).toBeUndefined()
    expect(withLogistics.ShipToCode).toBeUndefined()
    expect(withLogistics.U_Warehouse).toBeUndefined()

    const cleared = applyLogisticsToPo({ ShipToCode: 'C000030', U_Warehouse: 'Store1' }, {
      dispatchTo: 'C000030',
    })
    expect(cleared.ShipToCode).toBeUndefined()
    expect(cleared.U_Warehouse).toBeUndefined()
    expect(cleared.U_CardCode).toBe('C000030')

    const read = readLogisticsFromPo({
      U_CardCode: 'C000001',
      U_DisID: 'DISP-99',
      U_DispachAdd: 'Pune plant',
      U_SHIPTO: 'Ravi Kumar (9876543210)',
      U_PRI_BAS: 'F.O.R.',
      U_TransMode: '1',
      ShipToCode: 'SHOULD-NOT-USE',
    })
    expect(read.dispatchTo).toBe('C000001')
    expect(read.dispatchId).toBe('DISP-99')
    expect(read.dispatchAddress).toBe('Pune plant')
    expect(read.contactPerson).toBe('Ravi Kumar (9876543210)')
    expect(read.priceBasis).toBe('F.O.R.')
    expect(read.modeOfTransport).toBe('1')
  })
})

describe('PBBPL dispatch location ↔ warehouse', () => {
  it('maps Factory / Office / BP Loc to Store1 / Store5 / PBPL(S)', () => {
    expect(warehouseForDispatchLocation('Factory')).toBe('Store1')
    expect(warehouseForDispatchLocation('Office')).toBe('Store5')
    expect(warehouseForDispatchLocation('BP Loc')).toBe('PBPL(S)')
    expect(warehouseForDispatchLocation('Other')).toBeUndefined()
  })

  it('reverse-maps warehouses back to location type', () => {
    expect(dispatchLocationForWarehouse('Store1')).toBe('Factory')
    expect(dispatchLocationForWarehouse('Store5')).toBe('Office')
    expect(dispatchLocationForWarehouse('PBPL(S)')).toBe('BP Loc')
    expect(dispatchLocationForWarehouse('DRP')).toBeUndefined()
  })

  it('applies only for PBBPL company databases', () => {
    expect(usesPbbplDispatchLocationMapping('PBBPL_UAT')).toBe(true)
    expect(usesPbbplDispatchLocationMapping('PBBPL_LIVE')).toBe(true)
    expect(usesPbbplDispatchLocationMapping('OTHER_DB')).toBe(false)
  })
})
