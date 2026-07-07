import { useCallback, useMemo, useState } from 'react'
import { Eye } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, DataTable, type DataTableColumn } from '@/Components/ui'
import { getCardCodeFromRequest } from '@/helpers/approvalUtils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { listMyApprovalRequests, type ApprovalRequest } from '@/Requests/approvals'

const extractors = {
  cardCodes: (row: ApprovalRequest) => getCardCodeFromRequest(row),
}

export function MyApprovalRequestsPage() {
  const [viewRow, setViewRow] = useState<ApprovalRequest | null>(null)

  const fetchRequests = useCallback(
    (request: Parameters<typeof listMyApprovalRequests>[0]) => listMyApprovalRequests(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchRequests, extractors)

  const columns = useMemo<DataTableColumn<ApprovalRequest>[]>(() => [
    { key: 'id', header: 'ID', sortable: true, filterable: true, accessor: (r) => r.id },
    { key: 'documentType', header: 'Document Type', sortable: true, filterable: true, accessor: (r) => r.documentType },
    {
      key: 'cardCode',
      header: 'Business Partner',
      accessor: (r) => {
        const code = getCardCodeFromRequest(r)
        return formatCodeWithName(code, lookupMaps.businessPartners[code])
      },
    },
    { key: 'overallStatus', header: 'Status', render: (r) => <Badge>{r.overallStatus}</Badge> },
    { key: 'sapResponseDocNum', header: 'SAP Doc No', accessor: (r) => r.sapResponseDocNum },
    { key: 'sapResponseDocEntry', header: 'SAP Doc Entry', accessor: (r) => r.sapResponseDocEntry },
    { key: 'failureReason', header: 'Failure Reason', accessor: (r) => r.failureReason },
    { key: 'createdAt', header: 'Created', sortable: true, accessor: (r) => new Date(r.createdAt).toLocaleString() },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => (
        <RowActionButton
          title="View request"
          icon={<Eye className={rowActionIconClassName} />}
          onClick={() => setViewRow(row)}
        />
      ),
    },
  ], [lookupMaps])

  return (
    <div className="space-y-6">
      <PageHeader title="My Approval Requests" description="Track your submitted approval requests" />
      <DataTable columns={columns} fetchData={fetchData} getRowKey={(r) => r.id} initialSorts={[{ field: 'createdAt', direction: 'desc' }]} />
      <RequestViewDialog
        request={viewRow}
        readOnly
        onClose={() => setViewRow(null)}
        onCompleted={() => setViewRow(null)}
      />
    </div>
  )
}
