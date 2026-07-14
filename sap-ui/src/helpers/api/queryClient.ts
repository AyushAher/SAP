import { QueryClient } from '@tanstack/react-query'
import { API_CACHE_TTL_MS } from '@/helpers/api/requestCache'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: API_CACHE_TTL_MS,
      gcTime: API_CACHE_TTL_MS * 2,
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
})
