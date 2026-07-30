import { useCallback, useEffect, useMemo, useState } from 'react'
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
  syncNewPurchaseOrdersFromSap,
  syncPurchaseOrderFromSap,
  type PurchaseOrder,
} from '@/Requests/purchaseOrders'

const extractors = {
  projectCodes: (row: PurchaseOrder) => row.Project,
  cardCodes: (row: PurchaseOrder) => row.CardCode,
}

function formatPoValue(value?: number): string {
  if (value == null) return '—'
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function PurchaseOrderListPage() {
  const fetchOrders = usePurchaseOrderListFetcher()
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)
  const [tableKey, setTableKey] = useState(0)
  const [syncingNew, setSyncingNew] = useState(false)
  const [syncingDocEntry, setSyncingDocEntry] = useState<number | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncProgress, setSyncProgress] = useState<string | null>(null)
  const [branchMap, setBranchMap] = useState<Record<number, string>>({})

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

  const handleSyncNew = useCallback(async () => {
    setSyncingNew(true)
    setSyncError(null)
    setSyncProgress(null)
    try {
      const result = await syncNewPurchaseOrdersFromSap((progress) => {
        // The sync runs in bounded batches, so show running totals while it continues.
        setSyncProgress(
          progress.upsertedCount > 0
            ? `Synced ${progress.upsertedCount} purchase order(s) so far…`
            : null,
        )
      })
      toast.success(result.message)
      setTableKey((k) => k + 1)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Sync failed'
      setSyncError(message)
      toast.error(message)
    } finally {
      setSyncingNew(false)
      setSyncProgress(null)
    }
  }, [])

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
        const actionsDisabled = docEntry == null || syncingNew || syncingDocEntry != null

        return (
          <RowActionsMenu
            items={[
              {
                key: 'sync',
                label: 'Sync from SAP',
                disabled: actionsDisabled,
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
  ], [lookupMaps, syncingDocEntry, syncingNew, handleSyncRow, resolveBranchLabel])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchase Orders"
        description="Local database is the read source. Sync new POs from SAP, or refresh a single row."
        action={
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              onClick={() => void handleSyncNew()}
              isLoading={syncingNew}
              disabled={syncingDocEntry != null}
              leftIcon={<RefreshCw className="h-4 w-4" />}
            >
              Sync new from SAP
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
