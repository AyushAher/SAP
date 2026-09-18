export {
  normalizePurchaseOrderHeader as normalizePurchaseRequestHeader,
  normalizePurchaseOrderLineFromApi as normalizePurchaseRequestLineFromApi,
  calculatePurchaseOrderTotals as calculatePurchaseRequestTotals,
} from '@/helpers/purchaseOrderForm'

export * from '@/helpers/purchaseOrderForm'

export function isSapYesFlag(value: unknown): boolean {
  const normalized = String(value ?? '').trim().toUpperCase()
  return normalized === 'TYES' || normalized === 'Y' || normalized === 'YES'
}

/** Cancelled or closed PRs cannot be updated; SAP rejects HTTP DELETE. */
export function isPurchaseRequestReadOnly(record: {
  Cancelled?: unknown
  DocumentStatus?: unknown
}): boolean {
  if (isSapYesFlag(record.Cancelled)) return true
  return String(record.DocumentStatus ?? '') === 'bost_Close'
}

export function purchaseRequestStatusLabel(record: {
  Cancelled?: unknown
  DocumentStatus?: unknown
}): string {
  if (isSapYesFlag(record.Cancelled)) return 'Cancelled'
  if (record.DocumentStatus === 'bost_Close') return 'Close'
  if (record.DocumentStatus === 'bost_Open') return 'Open'
  return String(record.DocumentStatus ?? '-')
}
