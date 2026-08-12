import { useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { FileDown, Pencil, RefreshCw } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Button, DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useDocumentSync } from '@/hooks/useDocumentSync'
import {
  enqueueFullProductionOrderSyncJob,
  getProductionOrderSyncStatus,
  listProductionOrders,
  syncProductionOrderFromSap,
  type ProductionOrder,
} from '@/Requests/productionOrders'
import { downloadPdfTemplate } from '@/Requests/pdf'

export function ProductionOrderListPage() {
  // The list is served from the local mirror, so codes already arrive with their resolved names
  // and no master-data enrichment round trip is needed.
  const fetchData = useCallback(
    (request: Parameters<typeof listProductionOrders>[0]) => listProductionOrders(request),
    [],
  )

  const {
    tableKey,
    syncingAll,
    syncingKey,
    syncError,
    syncProgress,
    handleSyncAll,
    handleSyncRow,
  } = useDocumentSync({
    enqueueFullSync: enqueueFullProductionOrderSyncJob,
    getSyncStatus: getProductionOrderSyncStatus,
    syncRow: syncProductionOrderFromSap,
  })

  const columns = useMemo<DataTableColumn<ProductionOrder>[]>(() => [
    { key: 'AbsoluteEntry', header: 'Entry', sortable: true, filterable: true, accessor: (r) => r.AbsoluteEntry },
    { key: 'DocumentNumber', header: 'Doc Num', sortable: true, filterable: true, accessor: (r) => r.DocumentNumber },
    {
      key: 'CustomerCode',
      header: 'Customer',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => formatCodeWithName(r.CustomerCode, r.CustomerName),
    },
    {
      key: 'ItemNumber',
      header: 'Item',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => formatCodeWithName(String(r.ItemNumber ?? r.ItemNo ?? ''), r.ProductDescription),
    },
    { key: 'PlannedQuantity', header: 'Qty', sortable: true, accessor: (r) => r.PlannedQuantity },
    {
      key: 'Project',
      header: 'Project',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => formatCodeWithName(r.Project, r.ProjectName),
    },
    {
      key: 'SalesOrderDocNum',
      header: 'Sales Order',
      sortable: true,
      filterable: true,
      accessor: (r) => r.SalesOrderDocNum ?? '—',
    },
    {
      key: 'Status',
      header: 'Status',
      sortable: true,
      filterable: true,
      accessor: (r) => String(r.Status ?? r.ProductionOrderStatus ?? ''),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => {
        const absoluteEntry = row.AbsoluteEntry
        const rowBusy = absoluteEntry != null && syncingKey === absoluteEntry
        return (
          <RowActions>
            <RowActionButton
              title="Sync from SAP"
              disabled={absoluteEntry == null || rowBusy}
              icon={<RefreshCw className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`} />}
              onClick={() => absoluteEntry != null && void handleSyncRow(absoluteEntry)}
            />
            <RowActionLink
              to={`${ROUTES.PRODUCTION_ORDER_FORM}/${row.AbsoluteEntry}`}
              title="Edit production order"
              icon={<Pencil className={rowActionIconClassName} />}
            />
            <RowActionButton
              title="Download PDF"
              icon={<FileDown className={rowActionIconClassName} />}
              onClick={() => downloadPdfTemplate('production-order-template.html')}
            />
          </RowActions>
        )
      },
    },
  ], [handleSyncRow, syncingKey])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Production Orders"
        description="Local database is the read source. Sync fills missing entries, imports new production orders, then refreshes the ones still open."
        action={(
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              onClick={() => void handleSyncAll()}
              isLoading={syncingAll}
              disabled={syncingKey != null}
              leftIcon={<RefreshCw className="h-4 w-4" />}
            >
              Sync from SAP
            </Button>
            <Link to={ROUTES.PRODUCTION_ORDER_FORM}>
              <Button>Add New</Button>
            </Link>
          </div>
        )}
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
        getRowKey={(r) => r.AbsoluteEntry ?? r.DocumentNumber ?? String(r.ItemNumber ?? '')}
        initialSorts={[{ field: 'AbsoluteEntry', direction: 'desc' }]}
        defaultPageSize={100}
        pageSizeOptions={[10, 20, 50, 100]}
      />
    </div>
  )
}
