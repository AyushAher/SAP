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

  it('requires transporter item for series 124', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      vendorSeries: PO_TN.transporterBpSeries,
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

  it('requires G/L on non-inventory item lines', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{ ItemCode: 'SRV-1', InventoryItem: 'tNO', Quantity: 1, UnitPrice: 1 }],
    })).toBe('Select G/L Account in line 1.')

    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{
        ItemCode: 'SRV-1',
        InventoryItem: 'tNO',
        AccountCode: '600000',
        Quantity: 1,
        UnitPrice: 1,
      }],
    })).toBeNull()
  })

  it('blocks forbidden G/L on item docs too, where inventory-item account is optional', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{
        ItemCode: 'RM-1',
        AccountCode: PO_TN.forbiddenGlAccount,
        Quantity: 1,
        UnitPrice: 1,
      }],
    })).toContain('_SYS00000001265')
  })

  it('accepts an item line with no G/L account, since SAP determines it', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      lines: [{ ItemCode: 'RM-1', Quantity: 1, UnitPrice: 1 }],
    })).toBeNull()
  })

  it('passes when TN rules satisfied', () => {
    expect(validatePurchaseOrderAgainstTn({
      ...base,
      vendorSeries: PO_TN.transporterBpSeries,
      trn: 'OPEN-1',
      disId: 'C000030',
      dispachAdd: 'Addr',
      poType: 'JOB',
      lines: [{
        ItemCode: PO_TN.transporterMandatoryItem,
        WarehouseCode: 'DRP',
        Quantity: 1,
        UnitPrice: 1,
        TaxCode: 'IGST18',
        HSNEntry: 42,
      }],
    })).toBeNull()
  })
})
