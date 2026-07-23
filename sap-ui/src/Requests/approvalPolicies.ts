import { apiGet, apiPost, apiPut, apiPatch, apiDelete } from '@/helpers/api/client'

export type ApprovalRequesterType = 'User' | 'Group'

export interface ApprovalPolicy {
  id: number
  documentType: string
  requesterType: ApprovalRequesterType
  requesterUserId?: number | null
  requesterName?: string
  requesterGroupId?: number | null
  requesterGroupName?: string
  isActive: boolean
  approvers: Array<{ approverUserId: number; priority: number }>
  rules: Array<{ fieldName: string; operator: string; value: string }>
}

export type UpsertApprovalPolicyPayload = {
  documentType: string
  requesterType: ApprovalRequesterType
  requesterUserId?: number | null
  requesterGroupId?: number | null
  approvers: ApprovalPolicy['approvers']
  rules: ApprovalPolicy['rules']
}

export async function getApprovalPolicies() {
  return apiGet<ApprovalPolicy[]>('/approval-policies')
}

export async function createApprovalPolicy(payload: UpsertApprovalPolicyPayload) {
  return apiPost('/approval-policies', payload)
}

export async function updateApprovalPolicy(id: number, payload: UpsertApprovalPolicyPayload) {
  return apiPut(`/approval-policies/${id}`, payload)
}

export async function deleteApprovalPolicy(id: number) {
  return apiDelete(`/approval-policies/${id}`)
}

export async function setApprovalPolicyActive(id: number, isActive: boolean) {
  return apiPatch(`/approval-policies/${id}/active`, { isActive })
}

export interface ApprovalPolicyMetadata {
  documentTypes: string[]
  fields: Record<string, string[]>
  operators: string[]
}

export async function getApprovalPolicyMetadata(): Promise<ApprovalPolicyMetadata> {
  const data = await apiGet<Record<string, unknown>>('/approval-policies/metadata')
  const documentTypes = (data.documentTypes ?? data.DocumentTypes ?? []) as string[]
  const fields = (data.fields ?? data.Fields ?? {}) as Record<string, string[]>
  const operators = (data.operators ?? data.Operators ?? []) as string[]
  return { documentTypes, fields, operators }
}
