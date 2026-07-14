import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getStageWisePaymentPageData,
  type StageWisePaymentPageData,
} from '@/Requests/stageWisePayments'
import { API_CACHE_TTL_MS } from '@/helpers/api/requestCache'

export const stageWisePaymentKeys = {
  all: ['stage-wise-payments'] as const,
  pageData: (poDocEntry: number) =>
    [...stageWisePaymentKeys.all, 'page-data', poDocEntry] as const,
}

export function useStageWisePaymentPageData(poDocEntry: number) {
  return useQuery<StageWisePaymentPageData>({
    queryKey: stageWisePaymentKeys.pageData(poDocEntry),
    queryFn: () => getStageWisePaymentPageData(poDocEntry),
    enabled: Number.isFinite(poDocEntry) && poDocEntry > 0,
    staleTime: API_CACHE_TTL_MS,
  })
}

export function useInvalidateStageWisePaymentPageData() {
  const queryClient = useQueryClient()
  return (poDocEntry?: number) => {
    if (poDocEntry != null && Number.isFinite(poDocEntry)) {
      return queryClient.invalidateQueries({ queryKey: stageWisePaymentKeys.pageData(poDocEntry) })
    }
    return queryClient.invalidateQueries({ queryKey: stageWisePaymentKeys.all })
  }
}
