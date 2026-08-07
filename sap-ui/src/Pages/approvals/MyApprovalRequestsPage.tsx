import { useCallback, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { AlertTriangle, Eye, RefreshCw } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { rowActionIconClassName } from '@/Components/shared/RowActions'
import { RowActionsMenu } from '@/Components/shared/RowActionsMenu'
import { Badge, DataTable, type DataTableColumn } from '@/Components/ui'
import {
  canRetrySapExecution,
  formatDocumentType,
  getApprovalStatusBadgeVariant,
  getBusinessPartnerDisplayFromRequest,
  getCardCodeFromRequest,
} from '@/helpers/approvalUtils'
import { toast } from '@/helpers/toast'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { listMyApprovalRequests, retrySapExecution, type ApprovalRequest } from '@/Requests/approvals'

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
  const [retryingId, setRetryingId] = useState<number | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const fetchRequests = useCallback(
    (request: Parameters<typeof listMyApprovalRequests>[0]) => listMyApprovalRequests(request),
    [refreshKey],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchRequests, extractors)

  const reload = () => setRefreshKey((k) => k + 1)

  const handleRetrySap = async (row: ApprovalRequest) => {
    setRetryingId(row.id)
    try {
      await retrySapExecution(row.id)
      toast.success(`Request #${row.id} posted to SAP successfully.`)
      reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'SAP retry failed')
    } finally {
      setRetryingId(null)
    }
  }

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
      accessor: (r) => getBusinessPartnerDisplayFromRequest(r, lookupMaps.businessPartners),
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
      render: (row) => {
        const retryEligible = canRetrySapExecution(row)
        const rowBusy = retryingId === row.id

        return (
          <RowActionsMenu
            items={[
              {
                key: 'view',
                label: 'View',
                icon: <Eye className={rowActionIconClassName} />,
                onClick: () => setViewRow(row),
              },
              {
                key: 'retry-sap',
                label: 'Retry SAP',
                disabled: !retryEligible || rowBusy,
                icon: (
                  <RefreshCw
                    className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`}
                  />
                ),
                onClick: () => void handleRetrySap(row),
              },
            ]}
          />
        )
      },
    },
  ], [lookupMaps, retryingId])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Approval Status Report"
        description="Track the status of your submitted approval requests"
      />
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
      <DataTable
        key={refreshKey}
        columns={columns}
        fetchData={fetchData}
        getRowKey={(r) => r.id}
        initialSorts={[{ field: 'createdAt', direction: 'desc' }]}
      />
      <RequestViewDialog
        request={viewRow}
        readOnly
        onClose={() => setViewRow(null)}
        onCompleted={() => setViewRow(null)}
      />
    </div>
  )
}
