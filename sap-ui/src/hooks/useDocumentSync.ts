import { useCallback, useState } from 'react'
import { toast } from '@/helpers/toast'

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
  /** Reads the current background sync status, on request only. */
  getSyncStatus?: () => Promise<DocumentSyncStatus | null>
  /** Refreshes one document from SAP. */
  syncRow: (key: number) => Promise<DocumentSyncRowResult>
}

/**
 * Drives the "Sync from SAP" affordances for a mirrored SAP document list. Nothing here runs on a
 * timer: a full sync is queued and left alone, and the list only reloads when the user refreshes or
 * syncs a row. `tableKey` changes on those reloads so callers can remount their DataTable without
 * making their fetch callback unstable.
 */
export function useDocumentSync({ enqueueFullSync, getSyncStatus, syncRow }: UseDocumentSyncOptions) {
  const [tableKey, setTableKey] = useState(0)
  const [startingSync, setStartingSync] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const [syncingKey, setSyncingKey] = useState<number | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncMessage, setSyncMessage] = useState<string | null>(null)

  const applyStatus = useCallback((status: DocumentSyncStatus | null) => {
    if (!status) {
      setSyncMessage(null)
      return
    }
    if (status.status === 'Failed') {
      setSyncMessage(null)
      setSyncError(status.message || 'Sync failed.')
      return
    }
    setSyncError(null)
    setSyncMessage(status.message || null)
  }, [])

  const handleRefresh = useCallback(async () => {
    setRefreshing(true)
    try {
      if (getSyncStatus) applyStatus(await getSyncStatus())
    } catch {
      // The status line is informational; reload the rows regardless.
    } finally {
      setTableKey((key) => key + 1)
      setRefreshing(false)
    }
  }, [applyStatus, getSyncStatus])

  const handleSyncAll = useCallback(async () => {
    if (!enqueueFullSync) return
    setSyncError(null)
    setStartingSync(true)
    try {
      const started = await enqueueFullSync()
      const message = started.message || 'Sync started in the background.'
      setSyncMessage(message)
      toast.success(message)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to start sync'
      setSyncMessage(null)
      setSyncError(message)
      toast.error(message)
    } finally {
      setStartingSync(false)
    }
  }, [enqueueFullSync])

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
    startingSync,
    refreshing,
    syncingKey,
    syncError,
    syncMessage,
    handleRefresh,
    handleSyncAll,
    handleSyncRow,
  }
}
