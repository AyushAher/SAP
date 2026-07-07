import { useCallback, useMemo } from 'react'
import { FileDown, Pencil } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { DataTable, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { downloadReceiptFromProductionPdf, listReceiptFromProduction, type ReceiptFromProductionRequest } from '@/Requests/receiptFromProduction'

const extractors = {
  itemCodes: (row: ReceiptFromProductionRequest) => row.itemNo,
  projectCodes: (row: ReceiptFromProductionRequest) => row.project,
  cardCodes: (row: ReceiptFromProductionRequest) => row.cardCode,
}

export function ReceiptFromProductionListPage() {
  const fetchRequests = useCallback(
    (request: Parameters<typeof listReceiptFromProduction>[0]) => listReceiptFromProduction(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchRequests, extractors)

  const columns = useMemo<DataTableColumn<ReceiptFromProductionRequest>[]>(() => [
    { key: 'id', header: 'Request #', sortable: true, filterable: true, accessor: (r) => r.id },
    {
      key: 'cardCode',
      header: 'Customer',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.cardCode, r.cardName ?? lookupMaps.businessPartners[r.cardCode]),
    },
    {
      key: 'project',
      header: 'Project',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.project, r.projectName ?? lookupMaps.projects[r.project]),
    },
    {
      key: 'itemNo',
      header: 'Item',
      sortable: true,
      filterable: true,
      accessor: (r) => formatCodeWithName(r.itemNo, r.itemName ?? lookupMaps.items[r.itemNo]),
    },
    { key: 'status', header: 'Status', sortable: true, filterable: true, accessor: (r) => r.status },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => {
        if (!row.id) return null;
        const id = row.id;
        return (
          <RowActions>
            <RowActionLink
              to={`${ROUTES.RECEIPT_FROM_PRODUCTION_FORM}/${id}`}
              title="Edit receipt request"
              icon={<Pencil className={rowActionIconClassName} />}
            />
            <RowActionButton
              title="Download PDF"
              icon={<FileDown className={rowActionIconClassName} />}
              onClick={() => downloadReceiptFromProductionPdf(id)}
            />
          </RowActions>
        );
      },
    },
  ], [lookupMaps])

  return (
    <div className="space-y-6">
      <PageHeader title="Receipt From Production" actionLabel="Add New" actionTo={ROUTES.RECEIPT_FROM_PRODUCTION_FORM} />
      <DataTable columns={columns} fetchData={fetchData} getRowKey={(r) => r.id ?? Math.random()} initialSorts={[{ field: 'id', direction: 'desc' }]} />
    </div>
  )
}
