import type { BadgeProps } from '@/Components/ui'
import type { ApprovalRequest, UserApproval } from '@/Requests/approvals'

export function parseRequestBody(requestBody?: string): Record<string, unknown> | null {
  if (!requestBody) return null
  try {
    return JSON.parse(requestBody) as Record<string, unknown>
  } catch {
    return null
  }
}

export function getCardCodeFromRequest(request: ApprovalRequest): string {
  const body = parseRequestBody(request.requestBody)
  const cardCode = body?.CardCode ?? body?.cardCode
  return typeof cardCode === 'string' ? cardCode : ''
}

export function formatApprovalLabel(key: string): string {
  return key.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export function formatApprovalValue(value: unknown): string {
  if (value === null || value === undefined) return ''
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (typeof value === 'number') return String(value)
  if (typeof value === 'string') {
    const date = Date.parse(value)
    if (!Number.isNaN(date) && value.length >= 8) {
      const parsed = new Date(date)
      if (parsed.getFullYear() > 1900 && parsed.getFullYear() < 2100) {
        return parsed.toLocaleString()
      }
    }
    return value
  }
  if (Array.isArray(value)) return value.map(formatApprovalValue).join(', ')
  return JSON.stringify(value)
}

export function canActOnRequest(request: ApprovalRequest, readOnly: boolean): boolean {
  if (readOnly) return false
  return request.overallStatus === 'Pending' || request.overallStatus === 'Forwarded'
}

/**
 * True when this approval finalizes a Payment request — at which point payment date, reference
 * number, and user remarks must all be captured before SAP will accept the outgoing payment.
 */
export function requiresPaymentFinalizationDetails(request: ApprovalRequest): boolean {
  return request.documentType === 'Payments' && !!request.isLastApproval
}

const DOCUMENT_TYPE_LABELS: Record<string, string> = {
  PurchaseOrder: 'Purchase Order',
  ProductionOrder: 'Production Order',
  Payments: 'Payment',
  StagewisePayments_DP: 'Stage-wise Payment (Down Payment)',
  InventoryItemsTransfer: 'Inventory Items Transfer',
  IssueForProduction: 'Issue For Production',
}

/** Friendly label for a raw ApprovalDocumentType enum value (e.g. "StagewisePayments_DP"). */
export function formatDocumentType(type?: string): string {
  if (!type) return '—'
  return DOCUMENT_TYPE_LABELS[type] ?? type.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2')
}

/** Maps an ApprovalStatus value to a consistent Badge color across all approval screens. */
export function getApprovalStatusBadgeVariant(status?: string): NonNullable<BadgeProps['variant']> {
  switch (status) {
    case 'Approved':
      return 'success'
    case 'Pending':
      return 'warning'
    case 'Forwarded':
      return 'primary'
    case 'Rejected':
    case 'Failed':
      return 'danger'
    default:
      return 'default'
  }
}

export function getApproverDisplayName(approval: UserApproval): string {
  return approval.user?.fullName || approval.user?.userName || `User #${approval.userId}`
}

/** Groups user approvals by priority level, sorted ascending — the shape multi-level approval UIs need. */
export function groupApprovalsByLevel(userApprovals: UserApproval[] = []): Array<{ priority: number; approvers: UserApproval[] }> {
  const byPriority = new Map<number, UserApproval[]>()
  for (const approval of userApprovals) {
    const bucket = byPriority.get(approval.priority) ?? []
    bucket.push(approval)
    byPriority.set(approval.priority, bucket)
  }
  return Array.from(byPriority.entries())
    .sort(([a], [b]) => a - b)
    .map(([priority, approvers]) => ({ priority, approvers }))
}
