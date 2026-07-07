import { useCallback, useMemo } from 'react'
import { Ban, Lock, Pencil } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { cancelInventoryTransfer, closeInventoryTransfer, listInventoryTransfers, type InventoryTransfer } from '@/Requests/inventoryTransfers'

const extractors = {
  cardCodes: (row: InventoryTransfer) => row.CardCode,
}

export function InventoryTransferListPage() {
  const fetchTransfers = useCallback(
    (request: Parameters<typeof listInventoryTransfers>[0]) => listInventoryTransfers(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchTransfers, extractors)

  const columns = useMemo<DataTableColumn<InventoryTransfer>[]>(() => [
    { key: 'DocEntry', header: 'Doc Entry', sortable: true, filterable: true, accessor: (r) => r.DocEntry },
    { key: 'DocDate', header: 'Date', sortable: true, accessor: (r) => r.DocDate },
    { key: 'FromWarehouse', header: 'From', sortable: true, filterable: true, accessor: (r) => r.FromWarehouse },
    { key: 'ToWarehouse', header: 'To', sortable: true, filterable: true, accessor: (r) => r.ToWarehouse },
    {
      key: 'CardCode',
      header: 'Business Partner',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => formatCodeWithName(r.CardCode, r.CardName ?? lookupMaps.businessPartners[r.CardCode ?? '']),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => row.DocEntry && (
        <RowActions>
          <RowActionLink
            to={`${ROUTES.INVENTORY_TRANSFER_FORM}/${row.DocEntry}`}
            title="Edit stock transfer"
            icon={<Pencil className={rowActionIconClassName} />}
          />
          <RowActionButton
            title="Close stock transfer"
            icon={<Lock className={rowActionIconClassName} />}
            onClick={() => closeInventoryTransfer(String(row.DocEntry)).then(() => window.location.reload())}
          />
          <RowActionButton
            title="Cancel stock transfer"
            variant="danger"
            icon={<Ban className={rowActionIconClassName} />}
            onClick={() => cancelInventoryTransfer(String(row.DocEntry)).then(() => window.location.reload())}
          />
        </RowActions>
      ),
    },
  ], [lookupMaps])

  return (
    <div className="space-y-6">
      <PageHeader title="Stock Transfer Requests" actionLabel="Add New" actionTo={ROUTES.INVENTORY_TRANSFER_FORM} />
      <DataTable columns={columns} fetchData={fetchData} getRowKey={(r) => r.DocEntry ?? Math.random()} initialSorts={[{ field: 'DocEntry', direction: 'desc' }]} />
    </div>
  )
}
