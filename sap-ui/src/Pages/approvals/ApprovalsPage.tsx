import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle, CheckCircle2, Eye, X } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, Button, DataTable, Modal, Textarea, type DataTableColumn } from '@/Components/ui'
import { formatDocumentType, getApprovalStatusBadgeVariant, getCardCodeFromRequest } from '@/helpers/approvalUtils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { bulkApprove, bulkReject, listPendingApprovals, type ApprovalRequest, type BulkActionResultItem } from '@/Requests/approvals'
import { getBatchByApprovalRequestId } from '@/Requests/stageWisePaymentBatches'

const extractors = {
  cardCodes: (row: ApprovalRequest) => getCardCodeFromRequest(row),
}

function BulkResultBanner({ results, onDismiss }: { results: BulkActionResultItem[]; onDismiss: () => void }) {
  const failures = results.filter((r) => r.error)
  if (failures.length === 0) return null
  return (
    <div className="flex items-start justify-between gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      <div className="flex items-start gap-2">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
        <div>
          <p className="font-medium">{failures.length} of {results.length} request(s) could not be processed:</p>
          <ul className="mt-1 list-disc pl-5">
            {failures.map((f) => (
              <li key={f.id}>Request #{f.id}: {f.error}</li>
            ))}
          </ul>
        </div>
      </div>
      <button type="button" onClick={onDismiss} className="shrink-0 text-amber-600 hover:text-amber-800" aria-label="Dismiss">
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}

export function ApprovalsPage() {
  const navigate = useNavigate()
  const [selected, setSelected] = useState<number[]>([])
  const [viewRow, setViewRow] = useState<ApprovalRequest | null>(null)
  const [bulkApproveOpen, setBulkApproveOpen] = useState(false)
  const [bulkRejectOpen, setBulkRejectOpen] = useState(false)
  const [bulkRejectComment, setBulkRejectComment] = useState('')
  const [bulkSubmitting, setBulkSubmitting] = useState(false)
  const [bulkResults, setBulkResults] = useState<BulkActionResultItem[]>([])
  const [refreshKey, setRefreshKey] = useState(0)

  const fetchApprovals = useCallback(
    (request: Parameters<typeof listPendingApprovals>[0]) => listPendingApprovals(request),
    [refreshKey],
  )
  const { fetchData, lookupMaps } = useEnrichedListFetch(fetchApprovals, extractors)

  const reload = () => setRefreshKey((k) => k + 1)
  const clearSelection = () => setSelected([])

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

  const runBulkApprove = async () => {
    setBulkSubmitting(true)
    try {
      const results = await bulkApprove(selected)
      setBulkResults(results ?? [])
      clearSelection()
      reload()
      setBulkApproveOpen(false)
    } finally {
      setBulkSubmitting(false)
    }
  }

  const runBulkReject = async () => {
    setBulkSubmitting(true)
    try {
      const results = await bulkReject(selected, bulkRejectComment || 'Rejected')
      setBulkResults(results ?? [])
      clearSelection()
      setBulkRejectOpen(false)
      setBulkRejectComment('')
      reload()
    } finally {
      setBulkSubmitting(false)
    }
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
          aria-label={`Select request ${r.id}`}
        />
      ),
    },
    { key: 'id', header: 'ID', sortable: true, filterable: true, accessor: (r) => r.id },
    {
      key: 'documentType',
      header: 'Document Type',
      sortable: true,
      filterable: true,
      render: (r) => (
        <div className="flex items-center gap-2">
          <span>{formatDocumentType(r.documentType)}</span>
          {r.isLastApproval && <Badge variant="primary" size="sm">Final level</Badge>}
        </div>
      ),
    },
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
      render: (r) => <Badge variant={getApprovalStatusBadgeVariant(r.overallStatus)}>{r.overallStatus}</Badge>,
    },
    {
      key: 'sapDoc',
      header: 'SAP Doc',
      accessor: (r) => r.sapResponseDocNum ?? r.sapResponseDocEntry,
    },
    {
      key: 'issue',
      header: 'Issue',
      render: (r) => r.failureReason
        ? <span title={r.failureReason}><AlertTriangle className="h-4 w-4 text-red-500" /></span>
        : null,
    },
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

      <BulkResultBanner results={bulkResults} onDismiss={() => setBulkResults([])} />

      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={() => setBulkApproveOpen(true)} disabled={!selected.length}>Bulk Approve</Button>
        <Button variant="outline" onClick={() => setBulkRejectOpen(true)} disabled={!selected.length}>Bulk Reject</Button>
        {selected.length > 0 && (
          <>
            <span className="text-sm text-slate-500">{selected.length} selected</span>
            <Button variant="ghost" size="sm" onClick={clearSelection}>Clear selection</Button>
          </>
        )}
      </div>

      <DataTable key={refreshKey} columns={columns} fetchData={fetchData} getRowKey={(r) => r.id} initialSorts={[{ field: 'id', direction: 'desc' }]} />

      <RequestViewDialog
        request={viewRow}
        onClose={() => setViewRow(null)}
        onCompleted={reload}
      />

      <Modal isOpen={bulkApproveOpen} onClose={() => setBulkApproveOpen(false)} title="Confirm Bulk Approve">
        <div className="space-y-4">
          <div className="flex items-start gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-sm text-primary-800">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              You are about to approve <strong>{selected.length}</strong> request(s). Requests that finalize a
              document (e.g. final-level payment approvals) will be posted to SAP immediately.
            </span>
          </div>
          <div className="flex justify-end gap-3">
            <Button variant="outline" onClick={() => setBulkApproveOpen(false)}>Cancel</Button>
            <Button onClick={runBulkApprove} isLoading={bulkSubmitting}>Approve {selected.length}</Button>
          </div>
        </div>
      </Modal>

      <Modal isOpen={bulkRejectOpen} onClose={() => setBulkRejectOpen(false)} title="Bulk Reject">
        <div className="space-y-4">
          <Textarea
            label="Comment"
            value={bulkRejectComment}
            onChange={(e) => setBulkRejectComment(e.target.value)}
            placeholder="Reason for rejection"
          />
          <div className="flex justify-end gap-3">
            <Button variant="outline" onClick={() => setBulkRejectOpen(false)}>Cancel</Button>
            <Button variant="danger" onClick={runBulkReject} isLoading={bulkSubmitting}>Reject {selected.length}</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
