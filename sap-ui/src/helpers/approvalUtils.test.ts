import { describe, expect, it } from 'vitest'
import {
  canActOnRequest,
  formatApprovalLabel,
  formatApprovalValue,
  getCardCodeFromRequest,
  parseRequestBody,
  requiresUtrOnApprove,
} from './approvalUtils'
import type { ApprovalRequest } from '@/Requests/approvals'

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
})
