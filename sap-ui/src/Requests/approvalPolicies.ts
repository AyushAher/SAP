import { apiGet, apiPost, apiPut, apiDelete } from '@/helpers/api/client'

export interface ApprovalPolicy {
  id: number
  documentType: string
  requesterUserId: number
  requesterName?: string
  isActive: boolean
  approvers: Array<{ approverUserId: number; priority: number }>
  rules: Array<{ fieldName: string; operator: string; value: string }>
}

export async function getApprovalPolicies() {
  return apiGet<ApprovalPolicy[]>('/approval-policies')
}

export async function createApprovalPolicy(payload: Omit<ApprovalPolicy, 'id' | 'requesterName' | 'isActive'>) {
  return apiPost('/approval-policies', payload)
}

export async function updateApprovalPolicy(id: number, payload: Omit<ApprovalPolicy, 'id' | 'requesterName' | 'isActive'>) {
  return apiPut(`/approval-policies/${id}`, payload)
}

export async function deleteApprovalPolicy(id: number) {
  return apiDelete(`/approval-policies/${id}`)
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
