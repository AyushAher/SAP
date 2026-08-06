import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Banknote, Pencil, RefreshCw } from 'lucide-react'
import { toast } from '@/helpers/toast'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionsMenu } from '@/Components/shared/RowActionsMenu'
import { rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, Button, DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatDate } from '@/helpers/lib/utils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { usePurchaseOrderListFetcher } from '@/hooks/usePurchaseOrders'
import { getBranchesApi } from '@/Requests/auth'
import {
  enqueueFullPurchaseOrderSyncJob,
  getPurchaseOrderSyncStatus,
  syncPurchaseOrderFromSap,
  type PurchaseOrder,
} from '@/Requests/purchaseOrders'

const extractors = {
  projectCodes: (row: PurchaseOrder) => row.Project,
  cardCodes: (row: PurchaseOrder) => row.CardCode,
}

const SYNC_POLL_MS = 3000

function formatPoValue(value?: number): string {
  if (value == null) return '—'
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function isRunningStatus(status?: string | null): boolean {
  return (status ?? '').localeCompare('Running', undefined, { sensitivity: 'accent' }) === 0
}

export function PurchaseOrderListPage() {
  const fetchOrders = usePurchaseOrderListFetcher()
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)
  const [tableKey, setTableKey] = useState(0)
  const [syncingAll, setSyncingAll] = useState(false)
  const [syncingDocEntry, setSyncingDocEntry] = useState<number | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncProgress, setSyncProgress] = useState<string | null>(null)
  const [branchMap, setBranchMap] = useState<Record<number, string>>({})
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
      const status = await getPurchaseOrderSyncStatus()
      if (!status) return

      if (isRunningStatus(status.status)) {
        setSyncingAll(true)
        setSyncProgress(status.message || 'Full sync running…')
        return
      }

      if (status.status === 'Succeeded') {
        finishSyncUi(status.message || 'Full sync completed.', null)
        return
      }

      if (status.status === 'Failed') {
        finishSyncUi(null, status.message || 'Full sync failed.')
        return
      }

      // Idle after we started a job — stop UI spinner.
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

  useEffect(() => {
    void getBranchesApi()
      .then((branches) => {
        const map: Record<number, string> = {}
        for (const branch of branches ?? []) {
          map[branch.id] = branch.name
        }
        setBranchMap(map)
      })
      .catch(() => setBranchMap({}))
  }, [])

  // Resume progress UI if a Hangfire job is already running when the page loads.
  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const status = await getPurchaseOrderSyncStatus()
        if (cancelled || !status || !isRunningStatus(status.status)) return
        setSyncingAll(true)
        setSyncProgress(status.message || 'Full sync running…')
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
    setSyncProgress('Starting full sync…')
    setSyncingAll(true)
    try {
      const started = await enqueueFullPurchaseOrderSyncJob()
      setSyncProgress(started.message || 'Full sync job queued…')
      startPolling()
      await pollSyncStatus()
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to start sync'
      finishSyncUi(null, message)
    }
  }, [finishSyncUi, pollSyncStatus, startPolling])

  const handleSyncRow = useCallback(async (docEntry: number) => {
    setSyncingDocEntry(docEntry)
    setSyncError(null)
    try {
      const result = await syncPurchaseOrderFromSap(docEntry)
      toast.success(result.message)
      setTableKey((k) => k + 1)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Row sync failed'
      setSyncError(message)
      toast.error(message)
    } finally {
      setSyncingDocEntry(null)
    }
  }, [])

  const resolveBranchLabel = useCallback(
    (bplId?: number) => {
      if (bplId == null) return '—'
      return branchMap[bplId] ?? String(bplId)
    },
    [branchMap],
  )

  const columns = useMemo<DataTableColumn<PurchaseOrder>[]>(() => [
    { key: 'DocEntry', header: 'Doc Entry', sortable: true, filterable: true, accessor: (r) => r.DocEntry },
    { key: 'DocNum', header: 'Doc Num', sortable: true, filterable: true, accessor: (r) => r.DocNum },
    {
      key: 'DocDate',
      header: 'PO Date',
      sortable: true,
      filterable: true,
      accessor: (r) => (r.DocDate ? formatDate(r.DocDate) : '—'),
    },
    {
      key: 'BPLId',
      header: 'Branch',
      sortable: true,
      filterable: true,
      accessor: (r) => resolveBranchLabel(r.BPLId),
    },
    {
      key: 'CardCode',
      header: 'Business Partner',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.CardCode, r.CardName ?? lookupMaps.businessPartners[r.CardCode ?? '']),
    },
    {
      key: 'Project',
      header: 'Project',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.Project, lookupMaps.projects[r.Project ?? '']),
    },
    {
      key: 'DocTotal',
      header: 'PO Value',
      sortable: true,
      headerClassName: 'text-right',
      cellClassName: 'text-right tabular-nums',
      accessor: (r) => formatPoValue(r.DocTotal),
    },
    {
      key: 'DocumentStatus',
      header: 'Status',
      sortable: true,
      filterable: true,
      render: (r) => (
        <Badge variant={r.DocumentStatus === 'bost_Open' ? 'success' : 'default'}>
          {r.DocumentStatus === 'bost_Close' ? 'Close' : r.DocumentStatus === 'bost_Open' ? 'Open' : r.DocumentStatus ?? '-'}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => {
        const docEntry = row.DocEntry
        const rowBusy = docEntry != null && syncingDocEntry === docEntry
        // Do not gate on syncingAll — a stuck Hangfire Running status was disabling every row Sync.
        // Only disable the row currently syncing so other rows stay actionable.
        const syncDisabled = docEntry == null || syncingDocEntry === docEntry

        return (
          <RowActionsMenu
            items={[
              {
                key: 'sync',
                label: 'Sync from SAP',
                disabled: syncDisabled,
                icon: (
                  <RefreshCw
                    className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`}
                  />
                ),
                onClick: () => docEntry != null && void handleSyncRow(docEntry),
              },
              {
                key: 'edit',
                label: 'Edit',
                to: `${ROUTES.PURCHASE_ORDER_FORM}/${row.DocEntry}`,
                icon: <Pencil className={rowActionIconClassName} />,
              },
              {
                key: 'payments',
                label: 'Payment stages',
                to: `/purchase-orders/${row.DocEntry}/payments`,
                icon: <Banknote className={rowActionIconClassName} />,
              },
            ]}
          />
        )
      },
    },
  ], [lookupMaps, syncingDocEntry, syncingAll, handleSyncRow, resolveBranchLabel])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchase Orders"
        description="Local database is the read source. Sync fills missing DocEntry gaps, then imports POs newer than the local max."
        action={
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              onClick={() => void handleSyncAll()}
              isLoading={syncingAll}
              disabled={syncingDocEntry != null}
              leftIcon={<RefreshCw className="h-4 w-4" />}
            >
              Sync from SAP
            </Button>
            <Link to={ROUTES.PURCHASE_ORDER_FORM}>
              <Button>Add New</Button>
            </Link>
          </div>
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
        fetchData={fetchData}
        getRowKey={(r) => r.DocEntry ?? r.DocNum ?? Math.random()}
        initialSorts={[{ field: 'DocEntry', direction: 'desc' }]}
        defaultPageSize={100}
        pageSizeOptions={[10, 20, 50, 100]}
      />
    </div>
  )
}
