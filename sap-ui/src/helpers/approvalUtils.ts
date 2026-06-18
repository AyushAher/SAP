import type { ApprovalRequest } from '@/Requests/approvals'

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

export function requiresUtrOnApprove(request: ApprovalRequest): boolean {
  return request.documentType === 'Payments' && !!request.isLastApproval
}
