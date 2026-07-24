import { useCallback, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { AlertTriangle, Eye } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, DataTable, type DataTableColumn } from '@/Components/ui'
import { formatDocumentType, getApprovalStatusBadgeVariant, getCardCodeFromRequest } from '@/helpers/approvalUtils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { listMyApprovalRequests, type ApprovalRequest } from '@/Requests/approvals'

const extractors = {
  cardCodes: (row: ApprovalRequest) => getCardCodeFromRequest(row),
}

const STATUS_FILTER_OPTIONS = [
  { value: 'Pending', label: 'Pending' },
  { value: 'Forwarded', label: 'Forwarded' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Failed', label: 'Failed' },
]

export function MyApprovalRequestsPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const flashMessage = (location.state as { message?: string } | null)?.message
  const [viewRow, setViewRow] = useState<ApprovalRequest | null>(null)
  const [banner, setBanner] = useState<string | null>(flashMessage ?? null)

  const fetchRequests = useCallback(
    (request: Parameters<typeof listMyApprovalRequests>[0]) => listMyApprovalRequests(request),
    [],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchRequests, extractors)

  const columns = useMemo<DataTableColumn<ApprovalRequest>[]>(() => [
    { key: 'id', header: 'ID', sortable: true, filterable: true, accessor: (r) => r.id },
    {
      key: 'documentType',
      header: 'Document Type',
      sortable: true,
      filterable: true,
      accessor: (r) => formatDocumentType(r.documentType),
    },
    {
      key: 'cardCode',
      header: 'Business Partner',
      accessor: (r) => {
        const code = getCardCodeFromRequest(r)
        return formatCodeWithName(code, lookupMaps.businessPartners[code])
      },
    },
    {
      key: 'overallStatus',
      header: 'Status',
      filterable: true,
      filterType: 'select',
      filterOptions: STATUS_FILTER_OPTIONS,
      filterOperator: 'eq',
      render: (r) => <Badge variant={getApprovalStatusBadgeVariant(r.overallStatus)}>{r.overallStatus}</Badge>,
    },
    { key: 'sapResponseDocNum', header: 'SAP Doc No', accessor: (r) => r.sapResponseDocNum },
    { key: 'sapResponseDocEntry', header: 'SAP Doc Entry', accessor: (r) => r.sapResponseDocEntry },
    {
      key: 'failureReason',
      header: 'Issue',
      render: (r) => r.failureReason
        ? <span title={r.failureReason} className="inline-flex items-center gap-1 text-red-600"><AlertTriangle className="h-4 w-4" /> Failed</span>
        : null,
    },
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
      {banner ? (
        <div
          className="rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900"
          role="status"
        >
          {banner}
          <button
            type="button"
            className="ml-3 underline"
            onClick={() => {
              setBanner(null)
              navigate(location.pathname, { replace: true, state: {} })
            }}
          >
            Dismiss
          </button>
        </div>
      ) : null}
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
