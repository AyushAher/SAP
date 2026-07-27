import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Banknote, Pencil, RefreshCw } from 'lucide-react'
import { toast } from '@/helpers/toast'
import { PageHeader } from '@/Components/shared/PageHeader'
import {
  RowActionButton,
  RowActionLink,
  RowActions,
  rowActionIconClassName,
} from '@/Components/shared/RowActions'
import { Badge, Button, DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { usePurchaseOrderListFetcher } from '@/hooks/usePurchaseOrders'
import {
  getPurchaseOrderSyncStatus,
  syncNewPurchaseOrdersFromSap,
  syncPurchaseOrderFromSap,
  type PurchaseOrder,
  type PurchaseOrderSyncResult,
} from '@/Requests/purchaseOrders'

const extractors = {
  projectCodes: (row: PurchaseOrder) => row.Project,
  cardCodes: (row: PurchaseOrder) => row.CardCode,
}

function formatSyncLabel(status: PurchaseOrderSyncResult | null): string {
  if (!status?.syncedAtUtc || status.syncedAtUtc.startsWith('0001'))
    return 'Not synced yet — use “Sync new from SAP” to import purchase orders.'
  const when = new Date(status.syncedAtUtc).toLocaleString()
  return `Last SAP sync: ${when} — ${status.message || `${status.upsertedCount} order(s)`}`
}

export function PurchaseOrderListPage() {
  const fetchOrders = usePurchaseOrderListFetcher()
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)
  const [tableKey, setTableKey] = useState(0)
  const [syncingNew, setSyncingNew] = useState(false)
  const [syncingDocEntry, setSyncingDocEntry] = useState<number | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [syncStatus, setSyncStatus] = useState<PurchaseOrderSyncResult | null>(null)

  useEffect(() => {
    void getPurchaseOrderSyncStatus()
      .then(setSyncStatus)
      .catch(() => setSyncStatus(null))
  }, [tableKey])

  const handleSyncNew = useCallback(async () => {
    setSyncingNew(true)
    setSyncError(null)
    try {
      const result = await syncNewPurchaseOrdersFromSap()
      setSyncStatus(result)
      toast.success(result.message)
      setTableKey((k) => k + 1)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Sync failed'
      setSyncError(message)
      toast.error(message)
    } finally {
      setSyncingNew(false)
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

  const columns = useMemo<DataTableColumn<PurchaseOrder>[]>(() => [
    { key: 'DocEntry', header: 'Doc Entry', sortable: true, filterable: true, accessor: (r) => r.DocEntry },
    { key: 'DocNum', header: 'Doc Num', sortable: true, filterable: true, accessor: (r) => r.DocNum },
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
    { key: 'DocTotal', header: 'PO Value', sortable: true, accessor: (r) => r.DocTotal },
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
        return (
          <RowActions>
            <RowActionButton
              title="Sync this PO from SAP"
              disabled={docEntry == null || syncingNew || syncingDocEntry != null}
              onClick={() => docEntry != null && void handleSyncRow(docEntry)}
              icon={
                <RefreshCw
                  className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`}
                />
              }
            />
            <RowActionLink
              to={`${ROUTES.PURCHASE_ORDER_FORM}/${row.DocEntry}`}
              title="Edit purchase order"
              icon={<Pencil className={rowActionIconClassName} />}
            />
            <RowActionLink
              to={`/purchase-orders/${row.DocEntry}/payments`}
              title="Payment stages"
              variant="primary"
              icon={<Banknote className={rowActionIconClassName} />}
            />
          </RowActions>
        )
      },
    },
  ], [lookupMaps, syncingDocEntry, syncingNew, handleSyncRow])

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
      <p className="text-sm text-slate-500">{formatSyncLabel(syncStatus)}</p>
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
      />
    </div>
  )
}
