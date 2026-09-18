import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Filter, FileText, Pencil } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Badge, Button, DataTable, Input, Modal, SapDateInput, Select, type DataTableColumn } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatPoDisplayDate, toIsoDateOnly } from '@/helpers/lib/utils'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { isPurchaseRequestReadOnly, purchaseRequestStatusLabel } from '@/helpers/purchaseRequestForm'
import { useEnrichedListFetch } from '@/hooks/useEnrichedListFetch'
import { usePurchaseRequestListFetcher } from '@/hooks/usePurchaseRequests'
import { getBranchesApi } from '@/Requests/auth'
import {
  downloadPurchaseRequestReportPdf,
  getPurchaseRequestBranchId,
  type PurchaseRequest,
} from '@/Requests/purchaseRequests'
import { toast } from '@/helpers/toast'
import type { Filter as ListFilter, PaginationRequest } from '@/types/api'
import type { SelectOption } from '@/types'

const extractors = {
  projectCodes: (row: PurchaseRequest) => row.Project,
}

function requiredDateOf(row: PurchaseRequest): string | undefined {
  const raw = row.RequiredDate ?? row.RequriedDate ?? row.DocDueDate ?? row.DueDate
  if (typeof raw === 'string' || raw instanceof Date) return toIsoDateOnly(raw)
  return undefined
}

function usernameOf(row: PurchaseRequest): string {
  const code = String(row.Requester ?? '').trim()
  return code || String(row.RequesterName ?? '').trim() || '—'
}

interface ReportFilterState {
  username: string
  dateFrom: string
  dateTo: string
  requiredFrom: string
  requiredTo: string
  projectFrom: string
  projectTo: string
  period: string
  branchId: string
}

const emptyFilters = (): ReportFilterState => ({
  username: '',
  dateFrom: '',
  dateTo: '',
  requiredFrom: '',
  requiredTo: '',
  projectFrom: '',
  projectTo: '',
  period: '',
  branchId: '',
})

function buildReportFilters(state: ReportFilterState): ListFilter[] {
  const filters: ListFilter[] = []
  if (state.username.trim()) {
    filters.push({ field: 'Username', operator: 'contains', value: state.username.trim() })
  }
  if (state.dateFrom) {
    filters.push({ field: 'DocDate', operator: 'gte', value: state.dateFrom })
  }
  if (state.dateTo) {
    filters.push({ field: 'DocDate', operator: 'lte', value: state.dateTo })
  }
  if (state.requiredFrom) {
    filters.push({ field: 'RequiredDate', operator: 'gte', value: state.requiredFrom })
  }
  if (state.requiredTo) {
    filters.push({ field: 'RequiredDate', operator: 'lte', value: state.requiredTo })
  }
  if (state.projectFrom.trim()) {
    filters.push({ field: 'Project', operator: 'gte', value: state.projectFrom.trim() })
  }
  if (state.projectTo.trim()) {
    filters.push({ field: 'Project', operator: 'lte', value: state.projectTo.trim() })
  }
  if (state.period.trim()) {
    filters.push({ field: 'Period', operator: 'eq', value: state.period.trim() })
  }
  if (state.branchId) {
    filters.push({ field: 'BPLId', operator: 'eq', value: state.branchId })
  }
  return filters
}

export function PurchaseRequestReportPage() {
  const fetchOrders = usePurchaseRequestListFetcher()
  const { fetchData: fetchEnriched, lookupMaps } = useEnrichedListFetch(fetchOrders, extractors)
  const [branchOptions, setBranchOptions] = useState<SelectOption[]>([])
  const [branchMap, setBranchMap] = useState<Record<number, string>>({})
  const [draft, setDraft] = useState<ReportFilterState>(emptyFilters)
  const [applied, setApplied] = useState<ReportFilterState>(emptyFilters)
  const [dialogOpen, setDialogOpen] = useState(true)
  const [tableKey, setTableKey] = useState(0)
  const [generatingPdf, setGeneratingPdf] = useState(false)

  useEffect(() => {
    void getBranchesApi()
      .then((branches) => {
        const map: Record<number, string> = {}
        const options: SelectOption[] = []
        for (const branch of branches ?? []) {
          map[branch.id] = branch.name
          options.push({ value: String(branch.id), label: branch.name })
        }
        setBranchMap(map)
        setBranchOptions(options)
      })
      .catch(() => {
        setBranchMap({})
        setBranchOptions([])
      })
  }, [])

  const reportFilters = useMemo(() => buildReportFilters(applied), [applied])

  const fetchData = useCallback(async (request: PaginationRequest) => {
    return fetchEnriched({
      ...request,
      filters: [...reportFilters, ...(request.filters ?? [])],
    })
  }, [fetchEnriched, reportFilters])

  const applyFilters = () => {
    setApplied(draft)
    setTableKey((k) => k + 1)
    setDialogOpen(false)
  }

  const handlePreviewPdf = async () => {
    setGeneratingPdf(true)
    try {
      const blob = await downloadPurchaseRequestReportPdf(reportFilters)
      const url = URL.createObjectURL(blob)
      window.open(url, '_blank', 'noopener')
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to generate report PDF')
    } finally {
      setGeneratingPdf(false)
    }
  }

  const columns = useMemo<DataTableColumn<PurchaseRequest>[]>(() => [
    { key: 'DocEntry', header: 'Doc Entry', sortable: true, accessor: (r) => r.DocEntry },
    { key: 'DocNum', header: 'Document No', sortable: true, accessor: (r) => r.DocNum },
    {
      key: 'DocDate',
      header: 'Posting Date',
      sortable: true,
      accessor: (r) => (r.DocDate ? formatPoDisplayDate(String(r.DocDate)) : '—'),
    },
    {
      key: 'BPLId',
      header: 'Branch',
      sortable: true,
      accessor: (r) => {
        const bplId = getPurchaseRequestBranchId(r)
        if (bplId == null) return '—'
        return branchMap[bplId] ?? String(bplId)
      },
    },
    {
      key: 'Project',
      header: 'Project',
      sortable: true,
      accessor: (r) => formatCodeWithName(r.Project, lookupMaps.projects[r.Project ?? '']),
    },
    {
      key: 'RequiredDate',
      header: 'Required Date',
      sortable: true,
      accessor: (r) => {
        const iso = requiredDateOf(r)
        return iso ? formatPoDisplayDate(iso) : '—'
      },
    },
    {
      key: 'DocumentStatus',
      header: 'Status',
      render: (r) => (
        <Badge variant={r.DocumentStatus === 'bost_Open' && !isPurchaseRequestReadOnly(r) ? 'success' : 'default'}>
          {purchaseRequestStatusLabel(r)}
        </Badge>
      ),
    },
    {
      key: 'Requester',
      header: 'Username',
      sortable: true,
      accessor: (r) => usernameOf(r),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (row) => (
        <Link to={`${ROUTES.PURCHASE_REQUEST_FORM}/${row.DocEntry}`} className="inline-flex text-primary-700 hover:underline">
          <Pencil className="mr-1 h-4 w-4" />
          Open
        </Link>
      ),
    },
  ], [branchMap, lookupMaps.projects])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchase Request Report"
        description="Filter purchase requests by username, posting date, required date, project, period, and branch."
        action={(
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setDialogOpen(true)} leftIcon={<Filter className="h-4 w-4" />}>
              Filters
            </Button>
            <Button
              onClick={() => void handlePreviewPdf()}
              isLoading={generatingPdf}
              leftIcon={<FileText className="h-4 w-4" />}
            >
              Preview PDF
            </Button>
          </div>
        )}
      />

      <DataTable
        key={tableKey}
        columns={columns}
        fetchData={fetchData}
        getRowKey={(r) => r.DocEntry ?? r.DocNum ?? Math.random()}
        initialSorts={[{ field: 'DocEntry', direction: 'desc' }]}
        defaultPageSize={100}
        pageSizeOptions={[10, 20, 50, 100]}
      />

      <Modal
        isOpen={dialogOpen}
        onClose={() => setDialogOpen(false)}
        title="Purchase Request filters"
        size="lg"
        footer={(
          <>
            <Button type="button" variant="outline" onClick={() => setDraft(emptyFilters())}>Clear</Button>
            <Button type="button" onClick={applyFilters}>Apply</Button>
          </>
        )}
      >
        <div className="grid gap-4 md:grid-cols-2">
          <Input
            label="Username"
            value={draft.username}
            onChange={(e) => setDraft((prev) => ({ ...prev, username: e.target.value }))}
            placeholder="SAP user code"
          />
          <Select
            label="Branch"
            options={branchOptions}
            value={draft.branchId}
            onChange={(value) => setDraft((prev) => ({ ...prev, branchId: value }))}
            placeholder="All branches"
            clearable
          />
          <SapDateInput
            label="Date from"
            value={draft.dateFrom}
            onChangeIso={(iso) => setDraft((prev) => ({ ...prev, dateFrom: iso }))}
          />
          <SapDateInput
            label="Date to"
            value={draft.dateTo}
            onChangeIso={(iso) => setDraft((prev) => ({ ...prev, dateTo: iso }))}
          />
          <SapDateInput
            label="Required by from"
            value={draft.requiredFrom}
            onChangeIso={(iso) => setDraft((prev) => ({ ...prev, requiredFrom: iso }))}
          />
          <SapDateInput
            label="Required by to"
            value={draft.requiredTo}
            onChangeIso={(iso) => setDraft((prev) => ({ ...prev, requiredTo: iso }))}
          />
          <Input
            label="Project from"
            value={draft.projectFrom}
            onChange={(e) => setDraft((prev) => ({ ...prev, projectFrom: e.target.value }))}
            placeholder="Project code"
          />
          <Input
            label="Project to"
            value={draft.projectTo}
            onChange={(e) => setDraft((prev) => ({ ...prev, projectTo: e.target.value }))}
            placeholder="Project code"
          />
          <Input
            label="Period"
            type="month"
            value={draft.period}
            onChange={(e) => setDraft((prev) => ({ ...prev, period: e.target.value }))}
            hint="Posting month (YYYY-MM)"
          />
        </div>
      </Modal>
    </div>
  )
}
