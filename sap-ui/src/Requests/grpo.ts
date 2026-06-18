import { apiGet, apiPost } from '@/helpers/api/client'

export async function getGrpoFromPo(docEntry: number) {
  return apiGet<Record<string, unknown>>(`/grpo/from-po/${docEntry}`)
}

export async function createGrpo(data: Record<string, unknown>) {
  return apiPost('/grpo', data)
}
