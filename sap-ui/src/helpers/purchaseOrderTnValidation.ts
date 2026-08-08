import type { PurchaseOrderLineItem } from '@/types/purchaseOrder'

/** Mirrors SP_SBO_Purchase_Order transaction notification rules for client-side gating. */
export const PO_TN = {
  noBuyerCode: -1,
  transporterBpSeries: 124,
  transporterMandatoryItem: 'SR3346300000000000',
  forbiddenGlAccount: '_SYS00000001265',
  drpWarehouses: ['DRP', 'DRP2'] as const,
  jobPoType: 'JOB',
} as const

/** SAP B1 PurchaseOrders.DocType */
export const PO_DOC_TYPE = {
  items: 'dDocument_Items',
  service: 'dDocument_Service',
} as const

export const PO_DOC_TYPE_OPTIONS = [
  { value: PO_DOC_TYPE.items, label: 'Item' },
  { value: PO_DOC_TYPE.service, label: 'Service' },
] as const

export const PO_TYPE_OPTIONS = [
  { value: 'MAT', label: 'Material' },
  { value: 'SER', label: 'Service' },
  { value: 'JOB', label: 'Job Work' },
] as const

export function isServicePoDocType(docType?: string | null): boolean {
  return (docType ?? '').trim() === PO_DOC_TYPE.service
}

export interface PoTnValidationInput {
  salesPersonCode?: number | null
  documentsOwner?: number | null
  poType?: string | null
  docType?: string | null
  trn?: string | null
  disId?: string | null
  dispachAdd?: string | null
  vendorSeries?: number | null
  lines: PurchaseOrderLineItem[]
}

export function validatePurchaseOrderAgainstTn(input: PoTnValidationInput): string | null {
  const buyer = input.salesPersonCode
  if (buyer == null || buyer === PO_TN.noBuyerCode) {
    return 'Please Select Buyer'
  }

  if (input.documentsOwner == null || !Number.isFinite(input.documentsOwner)) {
    return 'Please Select Approver'
  }

  const isService = isServicePoDocType(input.docType)

  if (!isService) {
    const usesDrp = input.lines.some((line) => {
      const wh = (line.WarehouseCode ?? '').trim().toUpperCase()
      return wh === 'DRP' || wh === 'DRP2'
    })
    if (usesDrp && (!input.disId?.trim() || !input.dispachAdd?.trim())) {
      return 'You can not select DRP warehouse in Purchase Order.'
    }
  }

  const poType = (input.poType ?? '').trim().toUpperCase()
  if (poType === PO_TN.jobPoType) {
    const missingProd = input.lines.some((line) => !line.U_ProdNo?.trim())
    if (missingProd) {
      return 'Either Production Order No OR Open Order Field is mandatory at row level.'
    }
  }

  if (!isService && input.vendorSeries === PO_TN.transporterBpSeries) {
    const hasMandatoryItem = input.lines.some(
      (line) => (line.ItemCode ?? '').trim() === PO_TN.transporterMandatoryItem,
    )
    if (!hasMandatoryItem) {
      return 'Item SR3346300000000000 Is Mandatory For Transporter. Please Add This Item In The Purchase Order.'
    }
  }

  for (let i = 0; i < input.lines.length; i++) {
    const line = input.lines[i]
    const tax = (line.TaxCode ?? '').trim()

    if (isService) {
      if (!line.AccountCode?.trim()) {
        return `Select G/L Account in line ${i + 1}.`
      }
      if (line.AccountCode.trim() === PO_TN.forbiddenGlAccount) {
        return 'Selection of G/L Account _SYS00000001265 is not allowed in Purchase Order rows.'
      }
      if (tax && (line.SACEntry == null || !Number.isFinite(line.SACEntry))) {
        return `You must select SAC in line ${i + 1}, since GST tax code is selected`
      }
      continue
    }

    if (tax && (line.HSNEntry == null || !Number.isFinite(line.HSNEntry))) {
      return `You must select HSN in line ${i + 1}, since GST tax code is selected`
    }
  }

  return null
}
