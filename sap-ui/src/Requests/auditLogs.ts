import { apiListPost } from '@/helpers/api/list'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface ActionAuditLogRow {
  id: number
  userId?: number | null
  userName?: string | null
  companyDb?: string | null
  httpMethod: string
  path: string
  action: string
  statusCode: number
  ipAddress?: string | null
  durationMs: number
  createdAt: string
}

export function listActionAuditLogs(
  request?: PaginationRequest,
): Promise<PaginationResponse<ActionAuditLogRow[]>> {
  return apiListPost<ActionAuditLogRow>('/audit-logs/list', request)
}
