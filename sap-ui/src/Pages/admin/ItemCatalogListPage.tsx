import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { toast } from '@/helpers/toast'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, Button, DataTable, type DataTableColumn } from '@/Components/ui'
import {
  enqueueFullItemSyncJob,
  getItemSyncStatus,
  syncItemFromSap,
  listItemsCatalog,
  type MasterItem,
} from '@/Requests/masters'

const SYNC_POLL_MS = 3000

function isRunningStatus(status?: string | null): boolean {
  return (status ?? '').localeCompare('Running', undefined, { sensitivity: 'accent' }) === 0
}

export function ItemCatalogListPage() {
  const [tableKey, setTableKey] = useState(0)
  const [syncingAll, setSyncingAll] = useState(false)
  const [syncingItemCode, setSyncingItemCode] = useState<string | null>(null)
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
    setTableKey((k) => k + 1)
  }, [stopPolling])

  const pollSyncStatus = useCallback(async () => {
    try {
      const status = await getItemSyncStatus()
      if (!status) return

      if (isRunningStatus(status.status)) {
        setSyncingAll(true)
        setSyncProgress(status.message || 'Catalog sync running…')
        return
      }

      if (status.status === 'Succeeded') {
        finishSyncUi(status.message || 'Catalog sync completed.', null)
        return
      }

      if (status.status === 'Failed') {
        finishSyncUi(null, status.message || 'Catalog sync failed.')
        return
      }

      stopPolling()
      setSyncingAll(false)
      setSyncProgress(null)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to read sync status'
      finishSyncUi(null, message)
    }
  }, [finishSyncUi, stopPolling])

  const startPolling = useCallback(() => {
    stopPolling()
    pollTimerRef.current = setInterval(() => {
      void pollSyncStatus()
    }, SYNC_POLL_MS)
  }, [pollSyncStatus, stopPolling])

  // Resume progress UI if a Hangfire job (or the nightly cron) is already running on page load.
  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const status = await getItemSyncStatus()
        if (cancelled || !status || !isRunningStatus(status.status)) return
        setSyncingAll(true)
        setSyncProgress(status.message || 'Catalog sync running…')
        if (pollTimerRef.current == null) {
          pollTimerRef.current = setInterval(() => {
            void pollSyncStatus()
          }, SYNC_POLL_MS)
        }
      } catch {
        // Ignore — user can still trigger sync manually.
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
    setSyncError(null)
    setSyncProgress('Starting full catalog sync…')
    setSyncingAll(true)
    try {
      const started = await enqueueFullItemSyncJob()
      setSyncProgress(started.message || 'Full sync job queued…')
      startPolling()
      await pollSyncStatus()
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to start sync'
      finishSyncUi(null, message)
    }
  }, [finishSyncUi, pollSyncStatus, startPolling])

  const handleSyncRow = useCallback(async (itemCode: string) => {
    setSyncingItemCode(itemCode)
    setSyncError(null)
    try {
      const result = await syncItemFromSap(itemCode)
      toast.success(result.message)
      setTableKey((k) => k + 1)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Item sync failed'
      setSyncError(message)
      toast.error(message)
    } finally {
      setSyncingItemCode(null)
    }
  }, [])

  const columns = useMemo<DataTableColumn<MasterItem>[]>(() => [
    { key: 'ItemCode', header: 'Item Code', sortable: true, filterable: true, accessor: (r) => r.ItemCode },
    { key: 'ItemName', header: 'Item Name', sortable: true, filterable: true, accessor: (r) => r.ItemName },
    {
      key: 'InventoryItem',
      header: 'Inventory Item',
      sortable: true,
      render: (r) => (
        <Badge variant={r.InventoryItem === 'tYES' ? 'success' : 'default'}>
          {r.InventoryItem === 'tYES' ? 'Yes' : r.InventoryItem === 'tNO' ? 'No' : '—'}
        </Badge>
      ),
    },
    { key: 'InventoryUom', header: 'Inventory UOM', sortable: true, filterable: true, accessor: (r) => r.InventoryUom || '—' },
    { key: 'PurchaseUnit', header: 'Purchase Unit', sortable: true, filterable: true, accessor: (r) => r.PurchaseUnit || '—' },
    {
      key: 'PurchaseItemsPerUnit',
      header: 'Items / Purchase Unit',
      headerClassName: 'text-right',
      cellClassName: 'text-right tabular-nums',
      accessor: (r) => r.PurchaseItemsPerUnit ?? '—',
    },
    { key: 'DefaultWarehouse', header: 'Default Warehouse', sortable: true, filterable: true, accessor: (r) => r.DefaultWarehouse || '—' },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => {
        const itemCode = row.ItemCode
        const rowBusy = itemCode != null && syncingItemCode === itemCode
        return (
          <RowActions>
            <RowActionButton
              title="Sync from SAP"
              disabled={itemCode == null || rowBusy}
              icon={<RefreshCw className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`} />}
              onClick={() => itemCode != null && void handleSyncRow(itemCode)}
            />
          </RowActions>
        )
      },
    },
  ], [syncingItemCode, handleSyncRow])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Item Catalog"
        description="Local database is the read source for item search. A nightly job syncs the full SAP item master; sync here for an immediate refresh."
        action={
          <Button
            variant="outline"
            onClick={() => void handleSyncAll()}
            isLoading={syncingAll}
            disabled={syncingItemCode != null}
            leftIcon={<RefreshCw className="h-4 w-4" />}
          >
            Sync from SAP
          </Button>
        }
      />
      {syncProgress && <p className="text-sm text-slate-500">{syncProgress}</p>}
      {syncError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {syncError}
        </div>
      )}
      <DataTable
        key={tableKey}
        columns={columns}
        fetchData={listItemsCatalog}
        getRowKey={(r) => r.ItemCode ?? Math.random()}
        initialSorts={[{ field: 'ItemCode', direction: 'asc' }]}
        defaultPageSize={50}
        pageSizeOptions={[20, 50, 100, 200]}
        emptyMessage="No items synced yet — click Sync from SAP to import the catalog."
      />
    </div>
  )
}
