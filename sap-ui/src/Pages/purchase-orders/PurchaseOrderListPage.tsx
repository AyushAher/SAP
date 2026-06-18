import { useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, Badge, DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { listPurchaseOrders, type PurchaseOrder } from '@/Requests/purchaseOrders'

const extractors = {
  projectCodes: (row: PurchaseOrder) => row.Project,
  cardCodes: (row: PurchaseOrder) => row.CardCode,
}

export function PurchaseOrderListPage() {
  const fetchOrders = useCallback(
    (request: Parameters<typeof listPurchaseOrders>[0]) => listPurchaseOrders(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)

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
      render: (row) => (
        <div className="flex gap-2">
          <Link to={`${ROUTES.PURCHASE_ORDER_FORM}/${row.DocEntry}`}>
            <Button size="sm" variant="outline">Edit</Button>
          </Link>
          <Link to={`/purchase-orders/${row.DocEntry}/payments`}>
            <Button size="sm">Payment Stages</Button>
          </Link>
        </div>
      ),
    },
  ], [lookupMaps])

  return (
    <div className="space-y-6">
      <PageHeader title="Purchase Orders" description="Manage SAP purchase orders" actionLabel="Add New" actionTo={ROUTES.PURCHASE_ORDER_FORM} />
      <DataTable
        columns={columns}
        fetchData={fetchData}
        getRowKey={(r) => r.DocEntry ?? r.DocNum ?? Math.random()}
        initialSorts={[{ field: 'DocEntry', direction: 'desc' }]}
      />
    </div>
  )
}
