import { useSyncExternalStore } from 'react'
import { BlockingLoader } from '@/Components/ui'
import { getApiLoadingCount, subscribeApiLoading } from '@/helpers/api/apiLoading'

export function ApiLoadingOverlay() {
  const activeRequests = useSyncExternalStore(
    subscribeApiLoading,
    getApiLoadingCount,
    () => 0,
  )

  return (
    <BlockingLoader
      visible={activeRequests > 0}
      label="Processing…"
      lockScroll={false}
    />
  )
}
