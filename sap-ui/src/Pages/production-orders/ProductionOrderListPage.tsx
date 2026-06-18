import { useCallback, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { listProductionOrders, type ProductionOrder } from '@/Requests/productionOrders'
import { downloadPdfTemplate } from '@/Requests/pdf'

const extractors = {
  itemCodes: (row: ProductionOrder) => String(row.ItemNumber ?? row.ItemNo ?? ''),
  projectCodes: (row: ProductionOrder) => row.Project,
  cardCodes: (row: ProductionOrder) => row.CustomerCode,
}

export function ProductionOrderListPage() {
  const fetchOrders = useCallback(
    (request: Parameters<typeof listProductionOrders>[0]) => listProductionOrders(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)

  const columns = useMemo<DataTableColumn<ProductionOrder>[]>(() => [
    { key: 'AbsoluteEntry', header: 'Entry', sortable: true, filterable: true, accessor: (r) => r.AbsoluteEntry },
    { key: 'DocumentNumber', header: 'Doc Num', sortable: true, filterable: true, accessor: (r) => r.DocumentNumber },
    {
      key: 'CustomerCode',
      header: 'Customer',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(
        r.CustomerCode,
        r.CustomerName ?? lookupMaps.businessPartners[r.CustomerCode ?? ''],
      ),
    },
    {
      key: 'ItemNumber',
      header: 'Item',
      sortable: true,
      filterable: true,
      accessor: (r) => {
        const code = String(r.ItemNumber ?? r.ItemNo ?? '')
        return formatCodeWithName(code, r.ProductDescription ?? lookupMaps.items[code])
      },
    },
    { key: 'PlannedQuantity', header: 'Qty', sortable: true, accessor: (r) => r.PlannedQuantity },
    {
      key: 'Project',
      header: 'Project',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.Project, r.ProjectName ?? lookupMaps.projects[r.Project ?? '']),
    },
    { key: 'Status', header: 'Status', sortable: true, filterable: true, accessor: (r) => String(r.Status ?? r.ProductionOrderStatus ?? '') },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => (
        <div className="flex gap-2">
          <Link to={`${ROUTES.PRODUCTION_ORDER_FORM}/${row.AbsoluteEntry}`}><Button size="sm" variant="outline">Edit</Button></Link>
          <Button size="sm" variant="outline" onClick={() => downloadPdfTemplate('production-order-template.html')}>PDF</Button>
        </div>
      ),
    },
  ], [lookupMaps])

  return (
    <div className="space-y-6">
      <PageHeader title="Production Orders" actionLabel="Add New" actionTo={ROUTES.PRODUCTION_ORDER_FORM} />
      <DataTable columns={columns} fetchData={fetchData} getRowKey={(r) => r.AbsoluteEntry ?? r.DocumentNumber ?? Math.random()} initialSorts={[{ field: 'AbsoluteEntry', direction: 'desc' }]} />
    </div>
  )
}
