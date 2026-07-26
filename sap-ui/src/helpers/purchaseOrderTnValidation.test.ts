import { describe, expect, it } from 'vitest'
import {
  validatePurchaseOrderAgainstTn,
  PO_TN,
  PO_DOC_TYPE,
} from '@/helpers/purchaseOrderTnValidation'

describe('validatePurchaseOrderAgainstTn', () => {
  const base = {
    salesPersonCode: 5,
    documentsOwner: 10,
    poType: 'MAT',
    docType: PO_DOC_TYPE.items,
    trn: '',
    disId: '',
    dispachAdd: '',
    vendorSeries: 1,
    lines: [{ ItemCode: 'A1', WarehouseCode: '01', Quantity: 1, UnitPrice: 1 }],
  }

  it('requires buyer', () => {
    expect(validatePurchaseOrderAgainstTn({ ...base, salesPersonCode: -1 })).toBe('Please Select Buyer')
    expect(validatePurchaseOrderAgainstTn({ ...base, salesPersonCode: null })).toBe('Please Select Buyer')
  })

  it('requires approver', () => {
    expect(validatePurchaseOrderAgainstTn({ ...base, documentsOwner: null })).toBe('Please Select Approver')
  })

  it('requires DRP dispatch fields', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{ ItemCode: 'A1', WarehouseCode: 'DRP', Quantity: 1, UnitPrice: 1 }],
    })).toBe('You can not select DRP warehouse in Purchase Order.')
  })

  it('requires prod no for JOB type', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      poType: 'JOB',
      lines: [{ ItemCode: 'A1', WarehouseCode: '01', Quantity: 1, UnitPrice: 1 }],
    })).toBe('Either Production Order No OR Open Order Field is mandatory at row level.')
  })

  it('requires TRN and transporter item for series 124', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      vendorSeries: PO_TN.transporterBpSeries,
      trn: '',
    })).toBe('Please Select Open Order from List in Purchase Order Row.')

    expect(validatePurchaseOrderAgainstTn({
      ...base,
      vendorSeries: PO_TN.transporterBpSeries,
      trn: 'OPEN-1',
      lines: [{ ItemCode: 'A1', WarehouseCode: '01', Quantity: 1, UnitPrice: 1 }],
    })).toContain('SR3346300000000000')
  })

  it('requires HSN when tax code is selected on item docs', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{ ItemCode: 'A1', WarehouseCode: '01', Quantity: 1, UnitPrice: 1, TaxCode: 'IGST18' }],
    })).toBe('You must select HSN in line 1, since GST tax code is selected')
  })

  it('requires G/L and SAC on service docs', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      docType: PO_DOC_TYPE.service,
      lines: [{ ItemDescription: 'Freight', Quantity: 1, UnitPrice: 100 }],
    })).toBe('Select G/L Account in line 1.')

    expect(validatePurchaseOrderAgainstTn({
      ...base,
      docType: PO_DOC_TYPE.service,
      lines: [{
        ItemDescription: 'Freight',
        AccountCode: '600000',
        Quantity: 1,
        UnitPrice: 100,
        TaxCode: 'IGST18',
      }],
    })).toBe('You must select SAC in line 1, since GST tax code is selected')
  })

  it('blocks forbidden G/L on service docs', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      docType: PO_DOC_TYPE.service,
      lines: [{
        ItemDescription: 'X',
        AccountCode: PO_TN.forbiddenGlAccount,
        Quantity: 1,
        UnitPrice: 1,
      }],
    })).toContain('_SYS00000001265')
  })

  it('passes when TN rules satisfied', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      vendorSeries: PO_TN.transporterBpSeries,
      trn: 'OPEN-1',
      disId: 'D1',
      dispachAdd: 'Addr',
      poType: 'JOB',
      lines: [{
        ItemCode: PO_TN.transporterMandatoryItem,
        WarehouseCode: 'DRP',
        Quantity: 1,
        UnitPrice: 1,
        TaxCode: 'IGST18',
        HSNEntry: 42,
        U_ProdNo: 'PO-1',
      }],
    })).toBeNull()
  })
})
