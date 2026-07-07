import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Eye } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Button, Badge, DataTable, Modal, Textarea, type DataTableColumn } from '@/Components/ui'
import { getCardCodeFromRequest } from '@/helpers/approvalUtils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { bulkApprove, bulkReject, listPendingApprovals, type ApprovalRequest } from '@/Requests/approvals'
import { getBatchByApprovalRequestId } from '@/Requests/stageWisePaymentBatches'

const extractors = {
  cardCodes: (row: ApprovalRequest) => getCardCodeFromRequest(row),
}

export function ApprovalsPage() {
  const navigate = useNavigate()
  const [selected, setSelected] = useState<number[]>([])
  const [viewRow, setViewRow] = useState<ApprovalRequest | null>(null)
  const [bulkRejectOpen, setBulkRejectOpen] = useState(false)
  const [bulkRejectComment, setBulkRejectComment] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)

  const fetchApprovals = useCallback(
    (request: Parameters<typeof listPendingApprovals>[0]) => listPendingApprovals(request),
    [refreshKey],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchApprovals, extractors)

  const reload = () => setRefreshKey((k) => k + 1)

  const handleViewRequest = async (row: ApprovalRequest) => {
    if (row.documentType === 'Payments') {
      const batch = await getBatchByApprovalRequestId(row.id)
      if (batch) {
        navigate(`/purchase-orders/${batch.poDocEntry}/payments/batch/approve/${row.id}`)
        return
      }
    }
    setViewRow(row)
  }

  const toggleSelect = (id: number) => {
    setSelected((prev) => prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id])
  }

  const columns = useMemo<DataTableColumn<ApprovalRequest>[]>(() => [
    {
      key: 'select',
      header: '',
      render: (r) => (
        <input
          type="checkbox"
          checked={selected.includes(r.id)}
          disabled={r.overallStatus !== 'Pending' && r.overallStatus !== 'Forwarded'}
          onChange={() => toggleSelect(r.id)}
        />
      ),
    },
    { key: 'id', header: 'ID', sortable: true, filterable: true, accessor: (r) => r.id },
    { key: 'documentType', header: 'Document Type', sortable: true, filterable: true, accessor: (r) => r.documentType },
    {
      key: 'cardCode',
      header: 'Business Partner',
      filterable: true,
      accessor: (r) => {
        const code = getCardCodeFromRequest(r)
        return formatCodeWithName(code, lookupMaps.businessPartners[code])
      },
    },
    {
      key: 'overallStatus',
      header: 'Status',
      render: (r) => <Badge>{r.overallStatus}</Badge>,
    },
    { key: 'sapResponseDocEntry', header: 'SAP Doc Number', accessor: (r) => r.sapResponseDocEntry },
    { key: 'sapResponseDocNum', header: 'SAP Doc Entry', accessor: (r) => r.sapResponseDocNum },
    { key: 'failureReason', header: 'Failure Reason', accessor: (r) => r.failureReason },
    { key: 'supportingData', header: 'Supporting Data', accessor: (r) => r.supportingData },
    { key: 'requester', header: 'Requester', filterable: true, filterOperator: 'contains', accessor: (r) => r.requesterUser?.fullName ?? r.requesterUser?.userName },
    { key: 'createdAt', header: 'Created At', sortable: true, accessor: (r) => new Date(r.createdAt).toLocaleString() },
    {
      key: 'actions',
      header: 'Action',
      render: (row) => (
        <RowActionButton
          title="View request"
          variant="primary"
          icon={<Eye className={rowActionIconClassName} />}
          onClick={() => void handleViewRequest(row)}
        />
      ),
    },
  ], [lookupMaps, selected])

  return (
    <div className="space-y-6">
      <PageHeader title="My Pending Approvals" description="Review and approve SAP document requests" />
      <div className="flex gap-3">
        <Button onClick={() => bulkApprove(selected).then(reload)} disabled={!selected.length}>Bulk Approve</Button>
        <Button variant="outline" onClick={() => setBulkRejectOpen(true)} disabled={!selected.length}>Bulk Reject</Button>
      </div>
      <DataTable key={refreshKey} columns={columns} fetchData={fetchData} getRowKey={(r) => r.id} initialSorts={[{ field: 'id', direction: 'desc' }]} />

      <RequestViewDialog
        request={viewRow}
        onClose={() => setViewRow(null)}
        onCompleted={reload}
      />

      <Modal isOpen={bulkRejectOpen} onClose={() => setBulkRejectOpen(false)} title="Bulk Reject">
        <div className="space-y-4">
          <Textarea
            label="Comment"
            value={bulkRejectComment}
            onChange={(e) => setBulkRejectComment(e.target.value)}
            placeholder="Reason for rejection"
          />
          <div className="flex gap-3">
            <Button
              variant="outline"
              onClick={async () => {
                await bulkReject(selected, bulkRejectComment || 'Rejected')
                setBulkRejectOpen(false)
                setBulkRejectComment('')
                reload()
              }}
            >
              Confirm Reject
            </Button>
            <Button variant="outline" onClick={() => setBulkRejectOpen(false)}>Cancel</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
