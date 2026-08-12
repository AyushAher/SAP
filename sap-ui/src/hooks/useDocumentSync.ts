import { useCallback, useEffect, useRef, useState } from 'react'
import { toast } from '@/helpers/toast'

const SYNC_POLL_MS = 3000

export interface DocumentSyncStatus {
  /** Idle | Running | Succeeded | Failed */
  status?: string
  message?: string
}

export interface DocumentSyncRowResult {
  message: string
}

export interface UseDocumentSyncOptions {
  /** Queues the background sync for the whole company. Omit for row-sync-only screens. */
  enqueueFullSync?: () => Promise<{ message?: string }>
  /** Reads the current background sync status. Required when enqueueFullSync is supplied. */
  getSyncStatus?: () => Promise<DocumentSyncStatus | null>
  /** Refreshes one document from SAP. */
  syncRow: (key: number) => Promise<DocumentSyncRowResult>
}

function isRunningStatus(status?: string | null): boolean {
  return (status ?? '').localeCompare('Running', undefined, { sensitivity: 'accent' }) === 0
}

/**
 * Drives the "Sync from SAP" affordances for a mirrored SAP document list: a background full
 * sync with progress polling, plus per-row refresh. `tableKey` changes when a sync finishes, so
 * callers can remount their DataTable to refetch without making their fetch callback unstable.
 */
export function useDocumentSync({ enqueueFullSync, getSyncStatus, syncRow }: UseDocumentSyncOptions) {
  const [tableKey, setTableKey] = useState(0)
  const [syncingAll, setSyncingAll] = useState(false)
  const [syncingKey, setSyncingKey] = useState<number | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncProgress, setSyncProgress] = useState<string | null>(null)
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const stopPolling = useCallback(() => {
    if (pollTimerRef.current != null) {
      clearInterval(pollTimerRef.current)
      pollTimerRef.current = null
    }
  }, [])

  const finishSyncUi = useCallback((message: string | null, error: string | null) => {
    stopPolling()
    setSyncingAll(false)
    setSyncProgress(null)
    if (error) {
      setSyncError(error)
      toast.error(error)
      return
    }
    setSyncError(null)
    if (message) toast.success(message)
    setTableKey((key) => key + 1)
  }, [stopPolling])

  const pollSyncStatus = useCallback(async () => {
    if (!getSyncStatus) return
    try {
      const status = await getSyncStatus()
      if (!status) return

      if (isRunningStatus(status.status)) {
        setSyncingAll(true)
        setSyncProgress(status.message || 'Sync running…')
        return
      }

      if (status.status === 'Succeeded') {
        finishSyncUi(status.message || 'Sync completed.', null)
        return
      }

      if (status.status === 'Failed') {
        finishSyncUi(null, status.message || 'Sync failed.')
        return
      }

      // Idle after we started a job — stop the spinner.
      stopPolling()
      setSyncingAll(false)
      setSyncProgress(null)
    } catch (err) {
      finishSyncUi(null, err instanceof Error ? err.message : 'Failed to read sync status')
    }
  }, [finishSyncUi, getSyncStatus, stopPolling])

  const startPolling = useCallback(() => {
    stopPolling()
    pollTimerRef.current = setInterval(() => {
      void pollSyncStatus()
    }, SYNC_POLL_MS)
  }, [pollSyncStatus, stopPolling])

  // Resume the progress UI when a background job is already running as the page loads.
  useEffect(() => {
    if (!getSyncStatus) return
    let cancelled = false
    void (async () => {
      try {
        const status = await getSyncStatus()
        if (cancelled || !status || !isRunningStatus(status.status)) return
        setSyncingAll(true)
        setSyncProgress(status.message || 'Sync running…')
        if (pollTimerRef.current == null) {
          pollTimerRef.current = setInterval(() => {
            void pollSyncStatus()
          }, SYNC_POLL_MS)
        }
      } catch {
        // Ignore — the user can still trigger a sync manually.
      }
    })()
    return () => {
      cancelled = true
      stopPolling()
    }
    // Only on mount: pollSyncStatus/stopPolling are stable enough for the interval callback.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleSyncAll = useCallback(async () => {
    if (!enqueueFullSync) return
    setSyncError(null)
    setSyncProgress('Starting sync…')
    setSyncingAll(true)
    try {
      const started = await enqueueFullSync()
      setSyncProgress(started.message || 'Sync job queued…')
      startPolling()
      await pollSyncStatus()
    } catch (err) {
      finishSyncUi(null, err instanceof Error ? err.message : 'Failed to start sync')
    }
  }, [enqueueFullSync, finishSyncUi, pollSyncStatus, startPolling])

  const handleSyncRow = useCallback(async (key: number) => {
    setSyncingKey(key)
    setSyncError(null)
    try {
      const result = await syncRow(key)
      toast.success(result.message)
      setTableKey((current) => current + 1)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Row sync failed'
      setSyncError(message)
      toast.error(message)
    } finally {
      setSyncingKey(null)
    }
  }, [syncRow])

  return {
    tableKey,
    syncingAll,
    syncingKey,
    syncError,
    syncProgress,
    handleSyncAll,
    handleSyncRow,
  }
}
