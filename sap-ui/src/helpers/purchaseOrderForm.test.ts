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
  resolvePurchaseUnit,
  toSapDocumentLine,
  usesPbbplDispatchLocationMapping,
  warehouseForDispatchLocation,
  withItemsPerUnit,
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

  it('sends payment term description as Value% Basic|GST Type Stage', () => {
    expect(buildPaymentTermDescription({ id: 1, type: 'Advance', basic: 20, stage: 'Within 30 Days' }))
      .toBe('20% Basic As Advance Within 30 Days')
    expect(buildPaymentTermDescription({ id: 11, type: 'Invoice', gst: 100 }))
      .toBe('100% GST Against Invoice')

    const payload = applyPaymentTermsToPo({}, [
      { id: 1, type: 'Advance', basic: 20, stage: 'Stage1' },
      { id: 11, type: 'TaxInvoice', gst: 18 },
    ])
    expect(payload.U_D1).toBe('20% Basic As Advance Stage1')
    expect(payload.U_D11).toBe('18% GST Against Tax Invoice')
  })

  it('uses SAP ValidValue descriptions for the type when available', () => {
    const labels = { Advance: 'As Advance', Proforma: 'Against Proforma' }
    expect(buildPaymentTermDescription({ id: 2, type: 'Running', basic: 30 }, labels))
      .toBe('30% Basic Against Proforma')
    expect(applyPaymentTermsToPo({}, [{ id: 1, type: 'Advance', basic: 10 }], labels).U_D1)
      .toBe('10% Basic As Advance')
  })

  it('does not repeat GST when the type description already starts with it', () => {
    expect(buildPaymentTermDescription({ id: 11, type: 'GstProforma', gst: 100 }))
      .toBe('100% GST against Proforma Invoice')
  })
})

describe('logistics SAP field mapping', () => {
  it('maps to U_DisID / U_DispachAdd / U_SHIPTO (not U_CardCode, not ShipToCode)', () => {
    const payload = applyLogisticsToPo({}, {
      dispatchTo: 'C000001',
      dispatchAddress: 'Pune plant',
      contactPerson: 'Ravi Kumar (9876543210)',
    })
    expect(payload.U_DisID).toBe('C000001')
    expect(payload.U_CardCode).toBeUndefined()
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
    expect(cleared.U_DisID).toBe('C000030')

    const read = readLogisticsFromPo({
      U_DisID: 'C000001',
      U_DispachAdd: 'Pune plant',
      U_SHIPTO: 'Ravi Kumar (9876543210)',
      U_PRI_BAS: 'F.O.R.',
      U_TransMode: '1',
      ShipToCode: 'SHOULD-NOT-USE',
    })
    expect(read.dispatchTo).toBe('C000001')
    expect(read.dispatchAddress).toBe('Pune plant')
    expect(read.contactPerson).toBe('Ravi Kumar (9876543210)')
    expect(read.priceBasis).toBe('F.O.R.')
    expect(read.modeOfTransport).toBe('1')
  })

  it('truncates the dispatch address to the SAP field size', () => {
    const payload = applyLogisticsToPo({}, { dispatchAddress: 'x'.repeat(200) })
    expect(String(payload.U_DispachAdd)).toHaveLength(120)
  })

  it('reads the dispatch partner from U_CardCode on purchase orders saved before the move', () => {
    const legacy = readLogisticsFromPo({ U_CardCode: 'C000030', U_DispachAdd: 'Pune plant' })
    expect(legacy.dispatchTo).toBe('C000030')

    const migrated = readLogisticsFromPo({ U_DisID: 'C000099', U_CardCode: 'C000030' })
    expect(migrated.dispatchTo).toBe('C000099')
  })

  it('never echoes a stale U_CardCode back to SAP', () => {
    const payload = applyLogisticsToPo({ U_CardCode: 'C000030' }, { dispatchTo: 'C000099' })
    expect(payload.U_DisID).toBe('C000099')
    expect(payload.U_CardCode).toBeUndefined()
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

describe('resolvePurchaseUnit', () => {
  it('prefers the unit text SAP stores in MeasureUnit', () => {
    expect(resolvePurchaseUnit({ MeasureUnit: 'KGS', UoMCode: 'Manual' })).toBe('KGS')
  })

  it('ignores the Manual placeholder SAP uses for items without a UoM group', () => {
    expect(resolvePurchaseUnit({ UoMCode: 'Manual', UomName: 'NOS' })).toBe('NOS')
    expect(resolvePurchaseUnit({ UoMCode: 'Manual' })).toBe('')
  })

  it('falls back to a real UoM group code', () => {
    expect(resolvePurchaseUnit({ UoMCode: 'BOX' })).toBe('BOX')
  })
})

describe('withItemsPerUnit', () => {
  it('drives stock qty from the factor the user typed', () => {
    const line = withItemsPerUnit({ Quantity: 1600, StockQty: 43.2 }, 0.075)
    expect(line.StockQty).toBe(120)
    expect(line.UnitsOfMeasurment).toBeCloseTo(0.075, 10)
    expect(line.UseBaseUnits).toBe('tNO')
  })

  it('marks the line as inventory UoM when one purchase unit is one stock unit', () => {
    expect(withItemsPerUnit({ Quantity: 5, StockQty: 20 }, 1).UseBaseUnits).toBe('tYES')
  })

  it('keeps the typed factor when there is no purchase qty to multiply', () => {
    const line = withItemsPerUnit({ Quantity: 0 }, 2.5)
    expect(line.UnitsOfMeasurment).toBe(2.5)
    expect(line.StockQty).toBeUndefined()
  })
})

describe('toSapDocumentLine', () => {
  const itemLine = {
    ItemCode: 'RM5703813500380',
    ItemDescription: 'BEAM 250 MM',
    Quantity: 1600,
    StockQty: 120,
    UnitsOfMeasurment: 0.075,
    MeasureUnit: 'KGS',
    UoMCode: 'KGS',
    WarehouseCode: 'Store1',
    TaxCode: 'IGST18',
    HSNEntry: 19,
    AccountCode: '_SYS00000000893',
  }

  it('sends the unit as MeasureUnit and withholds UoMCode for Manual UoM group items', () => {
    const payload = toSapDocumentLine(itemLine, { isService: false })
    expect(payload.MeasureUnit).toBeUndefined()
    expect(payload.AccountCode).toBeUndefined()
    expect(payload.UoMCode).toBeUndefined()
    expect(payload.UoMEntry).toBeUndefined()
    expect(payload.InventoryQuantity).toBe(120)
    expect(payload.UnitsOfMeasurment).toBe(0.075)
    expect(payload.UseBaseUnits).toBe('tNO')
  })

  it('sends warehouse LocationCode on item lines', () => {
    const payload = toSapDocumentLine({ ...itemLine, LocationCode: 4 }, { isService: false })
    expect(payload.WarehouseCode).toBe('Store1')
    expect(payload.LocationCode).toBe(4)
  })

  it('sends UoMCode with its entry when the item is on a real UoM group', () => {
    const payload = toSapDocumentLine({ ...itemLine, UoMEntry: 4, MeasureUnit: 'BOX' }, { isService: false })
    expect(payload.UoMCode).toBe('BOX')
    expect(payload.UoMEntry).toBe(4)
    expect(payload.MeasureUnit).toBeUndefined()
  })

  it('does not send a G/L account on item lines, so SAP keeps determining it', () => {
    expect(toSapDocumentLine(itemLine, { isService: false }).AccountCode).toBeUndefined()
    expect(toSapDocumentLine({ ...itemLine, AccountCode: '   ' }, { isService: false }).AccountCode).toBeUndefined()
  })

  it('falls back to the header project when the line has none', () => {
    const payload = toSapDocumentLine(itemLine, { isService: false, fallbackProject: 'PRJ-9' })
    expect(payload.ProjectCode).toBe('PRJ-9')
  })

  it('keeps service lines to account, SAC and amounts', () => {
    const payload = toSapDocumentLine(
      { ItemDescription: 'Engineering', AccountCode: '_SYS00000000677', Quantity: 1, SACEntry: 11, MeasureUnit: 'NOS' },
      { isService: true },
    )
    expect(payload.AccountCode).toBe('_SYS00000000677')
    expect(payload.SACEntry).toBe(11)
    expect(payload.MeasureUnit).toBeUndefined()
    expect(payload.ItemCode).toBeUndefined()
    expect(payload.WarehouseCode).toBeUndefined()
  })
})
