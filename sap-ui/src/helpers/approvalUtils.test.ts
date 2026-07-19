import { describe, expect, it } from 'vitest'
import {
  canActOnRequest,
  formatApprovalLabel,
  formatApprovalValue,
  formatDocumentType,
  getApprovalStatusBadgeVariant,
  getApproverDisplayName,
  getCardCodeFromRequest,
  groupApprovalsByLevel,
  parseRequestBody,
  requiresUtrOnApprove,
} from './approvalUtils'
import type { ApprovalRequest, UserApproval } from '@/Requests/approvals'

function makeRequest(overrides: Partial<ApprovalRequest> = {}): ApprovalRequest {
  return {
    id: '1',
    documentType: 'PurchaseOrder',
    overallStatus: 'Pending',
    requestBody: JSON.stringify({ CardCode: 'V10000' }),
    ...overrides,
  } as ApprovalRequest
}

describe('approvalUtils', () => {
  it('parseRequestBody returns parsed object for valid JSON', () => {
    expect(parseRequestBody('{"CardCode":"A1"}')).toEqual({ CardCode: 'A1' })
  })

  it('parseRequestBody returns null for invalid JSON', () => {
    expect(parseRequestBody('{bad')).toBeNull()
    expect(parseRequestBody(undefined)).toBeNull()
  })

  it('getCardCodeFromRequest reads CardCode from request body', () => {
    expect(getCardCodeFromRequest(makeRequest())).toBe('V10000')
    expect(getCardCodeFromRequest(makeRequest({ requestBody: '{"cardCode":"c2"}' }))).toBe('c2')
    expect(getCardCodeFromRequest(makeRequest({ requestBody: '{}' }))).toBe('')
  })

  it('formatApprovalLabel inserts spaces before capitals', () => {
    expect(formatApprovalLabel('DocTotal')).toBe('Doc Total')
  })

  it('formatApprovalValue formats booleans and ISO dates', () => {
    expect(formatApprovalValue(true)).toBe('Yes')
    expect(formatApprovalValue(false)).toBe('No')
    expect(formatApprovalValue('2024-06-01T10:00:00Z')).not.toBe('2024-06-01T10:00:00Z')
  })

  it('canActOnRequest respects read-only and status', () => {
    expect(canActOnRequest(makeRequest(), false)).toBe(true)
    expect(canActOnRequest(makeRequest({ overallStatus: 'Approved' }), false)).toBe(false)
    expect(canActOnRequest(makeRequest(), true)).toBe(false)
  })

  it('requiresUtrOnApprove is true only for final payment approvals', () => {
    expect(requiresUtrOnApprove(makeRequest({
      documentType: 'Payments',
      isLastApproval: true,
    }))).toBe(true)
    expect(requiresUtrOnApprove(makeRequest({
      documentType: 'Payments',
      isLastApproval: false,
    }))).toBe(false)
    expect(requiresUtrOnApprove(makeRequest({ documentType: 'PurchaseOrder' }))).toBe(false)
  })

  it('formatDocumentType uses friendly labels and falls back to spaced casing', () => {
    expect(formatDocumentType('PurchaseOrder')).toBe('Purchase Order')
    expect(formatDocumentType('StagewisePayments_DP')).toBe('Stage-wise Payment (Down Payment)')
    expect(formatDocumentType('SomeNewDocType')).toBe('Some New Doc Type')
    expect(formatDocumentType(undefined)).toBe('—')
  })

  it('getApprovalStatusBadgeVariant maps every status to a distinct color', () => {
    expect(getApprovalStatusBadgeVariant('Approved')).toBe('success')
    expect(getApprovalStatusBadgeVariant('Pending')).toBe('warning')
    expect(getApprovalStatusBadgeVariant('Forwarded')).toBe('primary')
    expect(getApprovalStatusBadgeVariant('Rejected')).toBe('danger')
    expect(getApprovalStatusBadgeVariant('Failed')).toBe('danger')
    expect(getApprovalStatusBadgeVariant('Unknown')).toBe('default')
  })

  it('getApproverDisplayName prefers full name, then user name, then a fallback', () => {
    const base: UserApproval = { userId: 5, approvalStatus: 'Pending', priority: 1 }
    expect(getApproverDisplayName({ ...base, user: { fullName: 'Jane Doe', userName: 'jdoe' } })).toBe('Jane Doe')
    expect(getApproverDisplayName({ ...base, user: { userName: 'jdoe' } })).toBe('jdoe')
    expect(getApproverDisplayName(base)).toBe('User #5')
  })

  it('groupApprovalsByLevel groups by priority and sorts ascending', () => {
    const approvals: UserApproval[] = [
      { userId: 2, priority: 2, approvalStatus: 'Pending' },
      { userId: 1, priority: 1, approvalStatus: 'Approved' },
      { userId: 3, priority: 1, approvalStatus: 'Pending' },
    ]
    const levels = groupApprovalsByLevel(approvals)
    expect(levels.map((l) => l.priority)).toEqual([1, 2])
    expect(levels[0].approvers).toHaveLength(2)
    expect(levels[1].approvers).toHaveLength(1)
  })
})
