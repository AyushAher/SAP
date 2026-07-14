import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Trash2, Ban, Download, Undo2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, rowActionIconClassName, rowActionsCellClassName } from '@/Components/shared/RowActions'
import {
  Button,
  Card,
  CardContent,
  Input,
  Badge,
  SearchableSelect,
  Select,
  Textarea,
} from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import type { SelectOption } from '@/types'
import {
  approveRequest,
  getApprovalPaymentContext,
  rejectRequest,
} from '@/Requests/approvals'
import {
  createStageWisePaymentBatch,
  updateStageWisePaymentBatch,
  submitStageWisePaymentBatch,
  withdrawStageWisePaymentBatch,
  updateBatchAdditionalDetails,
  getBatchByApprovalRequestId,
  getBatchByStageWisePaymentId,
  getBatchPageData,
  cancelStageWisePaymentBatch,
  deleteStageWisePaymentBatch,
  downloadStageWisePaymentBatchPdf,
  getStageWisePaymentBatch,
  type BatchPayload,
  type StageWisePaymentBatch,
} from '@/Requests/stageWisePaymentBatches'
import type { StageWisePaymentPageData } from '@/Requests/stageWisePayments'
import {
  applySequentialBatchRowAdjustments,
  batchRowRequiresApInvoice,
  formatAmount,
  isBatchPaymentAvailable,
  isPaymentTermSelectable,
  paymentTermLabel,
  resolveBatchRowPayable,
  validateBatchPaymentAmounts,
  validateBatchComposition,
  type BatchAdjustmentContext,
} from '@/helpers/stageWisePaymentCalculations'

type BatchMode = 'create' | 'view' | 'approval' | 'viewByPayment'

const PAYMENT_MODE_OPTIONS: SelectOption[] = [
  { value: 'pmtBankTransfer', label: 'Bank Transfer' },
  { value: 'pmtChecks', label: 'Check' },
  { value: 'pmtCreditCard', label: 'Credit Card' },
  { value: 'pmtCash', label: 'Cash' },
]

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

function batchAdditionalDetailsFromBatch(batch: StageWisePaymentBatch) {
  return {
    modeOfPayment: batch.modeOfPayment ?? 'pmtBankTransfer',
    account: batch.account ?? '',
    journalRemark: batch.journalRemark ?? '',
    referenceNo: batch.referenceNo ?? '',
    postingDate: batch.postingDate ? batch.postingDate.slice(0, 10) : todayIsoDate(),
    paymentDate: batch.paymentDate ? batch.paymentDate.slice(0, 10) : '',
  }
}

interface EditableBatchRow {
  key: string
  apInvoiceDocEntry: string
  paymentTermsTypes: string[]
  amount: string
  baseBalanceDue: number
  basePayable: number
  balanceDue: number
  payable: number
  notes: string
}

function newRowKey() {
  return `row-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`
}

function emptyRow(): EditableBatchRow {
  return {
    key: newRowKey(),
    apInvoiceDocEntry: '',
    paymentTermsTypes: [],
    amount: '',
    baseBalanceDue: 0,
    basePayable: 0,
    balanceDue: 0,
    payable: 0,
    notes: '',
  }
}

function resolveMode(params: {
  batchId?: string
  approvalRequestId?: string
  stageWisePaymentId?: string
}): BatchMode {
  if (params.approvalRequestId) return 'approval'
  if (params.stageWisePaymentId) return 'viewByPayment'
  if (params.batchId && params.batchId !== 'new') return 'view'
  return 'create'
}

function batchToRows(batch: StageWisePaymentBatch): EditableBatchRow[] {
  const rawRows: EditableBatchRow[] = batch.lines.map((line) => ({
    key: `line-${line.id ?? newRowKey()}`,
    apInvoiceDocEntry: line.apInvoiceDocEntry ?? '',
    paymentTermsTypes: line.paymentTermsTypes.map(String),
    amount: String(line.amount),
    baseBalanceDue: 0,
    basePayable: 0,
    balanceDue: line.balanceDue,
    payable: line.payable,
    notes: line.notes ?? '',
  }))

  const withBase = rawRows.map((row) => {
    if (!row.apInvoiceDocEntry) return row
    const firstIndex = rawRows.findIndex((r) => r.apInvoiceDocEntry === row.apInvoiceDocEntry)
    const firstRow = firstIndex >= 0 ? rawRows[firstIndex] : row
    return {
      ...row,
      baseBalanceDue: firstRow.balanceDue,
      basePayable: firstRow.payable,
    }
  })

  return applySequentialBatchRowAdjustments(withBase)
}

export function StageWisePaymentBatchPage() {
  const navigate = useNavigate()
  const { id, batchId, approvalRequestId, stageWisePaymentId } = useParams()
  const poDocEntry = Number(id)
  const mode = resolveMode({ batchId, approvalRequestId, stageWisePaymentId })

  const [pageData, setPageData] = useState<StageWisePaymentPageData | null>(null)
  const [batch, setBatch] = useState<StageWisePaymentBatch | null>(null)
  const [rows, setRows] = useState<EditableBatchRow[]>([emptyRow()])
  const [sharedBank, setSharedBank] = useState('')
  const [sharedWtCode, setSharedWtCode] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [comment, setComment] = useState('')
  const [utrNo, setUtrNo] = useState('')
  const [utrDate, setUtrDate] = useState('')
  const [modeOfPayment, setModeOfPayment] = useState('pmtBankTransfer')
  const [account, setAccount] = useState('')
  const [journalRemark, setJournalRemark] = useState('')
  const [referenceNo, setReferenceNo] = useState('')
  const [postingDate, setPostingDate] = useState(todayIsoDate)
  const [paymentDate, setPaymentDate] = useState('')

  const isApproval = mode === 'approval'
  const readOnly = isApproval || (batch ? Boolean(batch.readOnly) : mode !== 'create')
  const canAct = batch?.canApprove ?? false
  const needsUtr = isApproval && (batch?.isLastApproval ?? false)
  const canWithdraw = !isApproval && Boolean(batch?.canWithdraw)
  const canSubmitExisting = !isApproval && Boolean(batch?.canSubmit)
  const isEditingExisting = Boolean(batch?.id) && !readOnly
  const canEditAdditionalDetails = !batch
    ? !readOnly
    : Boolean(batch.canEditAdditionalDetails)
  const additionalDetailsReadOnly = !canEditAdditionalDetails || loading
  const sapPaymentDetailsReadOnly = additionalDetailsReadOnly || Boolean(batch?.hasSapOutgoingPayment)
  const showAdditionalDetailsSave = Boolean(batch?.id) && canEditAdditionalDetails && readOnly

  const paymentTerms = pageData?.paymentTerms ?? []
  const selectableTerms = useMemo(
    () => paymentTerms.filter(isPaymentTermSelectable),
    [paymentTerms],
  )
  const paymentTermOptions: SelectOption[] = useMemo(
    () => selectableTerms
      .filter((t) => t.id != null)
      .map((t) => ({ value: String(t.id), label: paymentTermLabel(t) })),
    [selectableTerms],
  )

  const bankOptions: SelectOption[] = useMemo(
    () => (pageData?.banks ?? []).map((b) => ({ value: b.key, label: b.value })),
    [pageData?.banks],
  )

  const bankLabel = useCallback(
    (key?: string) => (key ? pageData?.bankLabels[key] ?? key : ''),
    [pageData?.bankLabels],
  )

  const accountOptions: SelectOption[] = useMemo(() => {
    if (!account || bankOptions.some((option) => option.value === account))
      return bankOptions
    return [
      ...bankOptions,
      { value: account, label: batch?.accountLabel ?? (bankLabel(account) || account) },
    ]
  }, [bankOptions, account, batch?.accountLabel, bankLabel])

  const wtCodeOptions: SelectOption[] = useMemo(
    () => (pageData?.withholdingTaxCodes ?? []).map((wt) => ({
      value: wt.wtCode,
      label: wt.wtName ?? wt.wtCode,
    })),
    [pageData?.withholdingTaxCodes],
  )

  const po = pageData?.purchaseOrder
  const batchPaymentEnabled = isBatchPaymentAvailable(po, selectableTerms, pageData?.apInvoices)
  const hasApInvoices = (pageData?.apInvoices?.length ?? 0) > 0

  const applyBatchAdditionalDetails = useCallback((batchData: StageWisePaymentBatch) => {
    const details = batchAdditionalDetailsFromBatch(batchData)
    setModeOfPayment(details.modeOfPayment)
    setAccount(details.account)
    setJournalRemark(details.journalRemark)
    setReferenceNo(details.referenceNo)
    setPostingDate(details.postingDate)
    setPaymentDate(details.paymentDate)
  }, [])

  const searchApInvoiceOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const term = search.trim().toLowerCase()
    const invoices = pageData?.apInvoices ?? []
    const filtered = term
      ? invoices.filter((inv) =>
          String(inv.DocNum ?? '').toLowerCase().includes(term)
          || String(inv.NumAtCard ?? '').toLowerCase().includes(term)
          || String(inv.DocEntry ?? '').includes(term))
      : invoices.slice(0, 20)
    return filtered.map((inv) => ({
      value: String(inv.DocEntry ?? ''),
      label: `${inv.DocNum ?? ''}:${inv.NumAtCard ?? ''}`,
    })).filter((o) => o.value)
  }, [pageData?.apInvoices])

  const recalculateRow = useCallback((row: EditableBatchRow): EditableBatchRow => {
    if (!pageData?.purchaseOrder || row.paymentTermsTypes.length === 0) {
      return { ...row, baseBalanceDue: 0, basePayable: 0, balanceDue: 0, payable: 0 }
    }

    const termIds = row.paymentTermsTypes.map(Number)
    const apInvoice = row.apInvoiceDocEntry
      ? pageData.apInvoices.find((inv) => String(inv.DocEntry ?? '') === row.apInvoiceDocEntry)
      : undefined
    const { balanceDue, payable } = resolveBatchRowPayable(
      pageData.purchaseOrder,
      pageData.paymentTerms,
      pageData.activeRecords,
      termIds,
      apInvoice,
      row.apInvoiceDocEntry,
      pageData.totalBasic,
    )

    return {
      ...row,
      baseBalanceDue: balanceDue,
      basePayable: payable,
      balanceDue,
      payable,
    }
  }, [pageData])

  const adjustmentContext = useMemo<BatchAdjustmentContext | undefined>(() => {
    if (!pageData?.purchaseOrder)
      return undefined
    return {
      po: pageData.purchaseOrder,
      paymentTerms: pageData.paymentTerms,
      activeRecords: pageData.activeRecords,
      totalBasic: pageData.totalBasic,
      apInvoices: pageData.apInvoices,
    }
  }, [pageData])

  const displayRows = useMemo(
    () => (adjustmentContext
      ? applySequentialBatchRowAdjustments(rows, adjustmentContext)
      : applySequentialBatchRowAdjustments(rows)),
    [rows, adjustmentContext],
  )

  const loadInitial = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await getBatchPageData(poDocEntry)
      setPageData(data)

      if (mode === 'approval' && approvalRequestId) {
        const batchData = await getBatchByApprovalRequestId(Number(approvalRequestId))
        if (!batchData) {
          setError('No batch payment found for this approval request.')
          return
        }
        setBatch(batchData)
        setRows(batchToRows(batchData))
        setSharedBank(batchData.lines[0]?.bank ?? '')
        setSharedWtCode(batchData.wtCode ?? '')
        applyBatchAdditionalDetails(batchData)
        if (batchData.canApprove) {
          const ctx = await getApprovalPaymentContext(Number(approvalRequestId))
          setUtrNo(ctx.utrNo ?? '')
          setUtrDate(ctx.utrDate ? new Date(ctx.utrDate).toISOString().slice(0, 10) : '')
        }
        return
      }

      if (mode === 'viewByPayment' && stageWisePaymentId) {
        const batchData = await getBatchByStageWisePaymentId(Number(stageWisePaymentId))
        setBatch(batchData)
        setRows(batchToRows(batchData))
        setSharedBank(batchData.lines[0]?.bank ?? '')
        setSharedWtCode(batchData.wtCode ?? '')
        applyBatchAdditionalDetails(batchData)
        return
      }

      if (mode === 'view' && batchId) {
        const batchData = await getStageWisePaymentBatch(Number(batchId))
        setBatch(batchData)
        setRows(batchToRows(batchData))
        setSharedBank(batchData.lines[0]?.bank ?? '')
        setSharedWtCode(batchData.wtCode ?? '')
        applyBatchAdditionalDetails(batchData)
        return
      }

      if (mode === 'create' && !isBatchPaymentAvailable(data.purchaseOrder, data.paymentTerms, data.apInvoices)) {
        setError('No payment types are configured for this purchase order.')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load batch payment data')
    } finally {
      setLoading(false)
    }
  }, [poDocEntry, mode, batchId, approvalRequestId, stageWisePaymentId, applyBatchAdditionalDetails])

  useEffect(() => {
    if (readOnly || !sharedBank || account) return
    setAccount(sharedBank)
  }, [readOnly, sharedBank, account])

  useEffect(() => {
    void loadInitial()
  }, [loadInitial])

  const updateRow = (key: string, patch: Partial<EditableBatchRow>) => {
    setRows((prev) => {
      const mergedRow = prev.find((r) => r.key === key)
      if (!mergedRow) return prev

      let nextPatch = { ...patch }
      if (patch.paymentTermsTypes !== undefined && pageData?.purchaseOrder) {
        const termIds = patch.paymentTermsTypes.map(Number)
        if (!batchRowRequiresApInvoice(pageData.purchaseOrder, pageData.paymentTerms, termIds)) {
          nextPatch = { ...nextPatch, apInvoiceDocEntry: '' }
        }
      }

      let nextRows = prev.map((r) => (r.key === key ? { ...r, ...nextPatch } : r))
      if (readOnly) return nextRows

      const updatedRow = nextRows.find((r) => r.key === key)!
      const shouldRecalc = patch.apInvoiceDocEntry !== undefined || patch.paymentTermsTypes !== undefined

      if (shouldRecalc) {
        if (updatedRow.paymentTermsTypes.length === 0) {
          nextRows = nextRows.map((r) => (r.key === key
            ? { ...updatedRow, baseBalanceDue: 0, basePayable: 0, balanceDue: 0, payable: 0 }
            : r))
        } else {
          const recalced = recalculateRow(updatedRow)
          nextRows = nextRows.map((r) => (r.key === key ? recalced : r))
        }
      }

      return adjustmentContext
        ? applySequentialBatchRowAdjustments(nextRows, adjustmentContext)
        : applySequentialBatchRowAdjustments(nextRows)
    })
  }

  const addRow = () => setRows((prev) => [...prev, emptyRow()])

  const removeRow = (key: string) => {
    setRows((prev) => (prev.length <= 1 ? prev : prev.filter((r) => r.key !== key)))
  }

  const totalAmount = displayRows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0)

  const buildPayload = (): BatchPayload | null => {
    if (!sharedBank) {
      setError('Please select a bank account.')
      return null
    }

    if (!account) {
      setError('Please select an account in Additional Details.')
      return null
    }

    if (!postingDate || !paymentDate) {
      setError('Posting date and payment date are required.')
      return null
    }

    if (!adjustmentContext) {
      setError('Payment data is not loaded.')
      return null
    }

    const compositionError = validateBatchComposition(displayRows, adjustmentContext)
    if (compositionError) {
      setError(compositionError)
      return null
    }

    const validationError = validateBatchPaymentAmounts(displayRows, adjustmentContext)
    if (validationError) {
      setError(validationError)
      return null
    }

    for (const row of displayRows) {
      const termIds = row.paymentTermsTypes.map(Number)
      const requiresAp = po && batchRowRequiresApInvoice(po, paymentTerms, termIds)
      if (requiresAp && !row.apInvoiceDocEntry) {
        setError('AP invoice is required for Invoice, Retention, or closed PO payment rows.')
        return null
      }
      if (row.paymentTermsTypes.length === 0) {
        setError('Each row must have at least one payment type selected.')
        return null
      }
    }

    return {
      poDocEntry,
      docNumber: po?.DocNum,
      wtCode: sharedWtCode || undefined,
      modeOfPayment,
      account,
      journalRemark: journalRemark || undefined,
      referenceNo: referenceNo || undefined,
      postingDate,
      paymentDate,
      lines: rows.map((row) => ({
        apInvoiceDocEntry: row.apInvoiceDocEntry || undefined,
        paymentTermsTypes: row.paymentTermsTypes.map(Number),
        bank: sharedBank,
        amount: Number(row.amount),
        notes: row.notes || undefined,
      })),
    }
  }

  const applyLoadedBatch = (batchData: StageWisePaymentBatch) => {
    setBatch(batchData)
    setRows(batchToRows(batchData))
    setSharedBank(batchData.lines[0]?.bank ?? '')
    setSharedWtCode(batchData.wtCode ?? '')
    applyBatchAdditionalDetails(batchData)
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (readOnly) return

    const payload = buildPayload()
    if (!payload) return

    setSubmitting(true)
    setError(null)
    setSuccessMessage(null)
    try {
      if (isEditingExisting && batch?.id) {
        const result = await submitStageWisePaymentBatch(batch.id, payload)
        applyLoadedBatch(result)
        setSuccessMessage('Batch payment submitted for approval.')
        navigate(`/purchase-orders/${poDocEntry}/payments/batch/${result.id}`, { replace: true })
      } else {
        const result = await createStageWisePaymentBatch(payload)
        applyLoadedBatch(result)
        setSuccessMessage('Batch payment request created.')
        navigate(`/purchase-orders/${poDocEntry}/payments/batch/${result.id}`, { replace: true })
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to submit batch payment')
    } finally {
      setSubmitting(false)
    }
  }

  const handleSaveDraft = async () => {
    if (readOnly || !batch?.id || !canSubmitExisting) return

    const payload = buildPayload()
    if (!payload) return

    setSubmitting(true)
    setError(null)
    setSuccessMessage(null)
    try {
      const result = await updateStageWisePaymentBatch(batch.id, payload)
      applyLoadedBatch(result)
      setSuccessMessage('Draft batch payment saved.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save batch payment')
    } finally {
      setSubmitting(false)
    }
  }

  const handleWithdraw = async () => {
    if (!batch?.id || !canWithdraw) return
    if (!window.confirm('Withdraw this approval request? You can edit the batch and submit again.')) return
    setSubmitting(true)
    setError(null)
    setSuccessMessage(null)
    try {
      const result = await withdrawStageWisePaymentBatch(batch.id)
      applyLoadedBatch(result)
      setSuccessMessage('Approval request withdrawn. Edit the batch and submit again.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to withdraw approval request')
    } finally {
      setSubmitting(false)
    }
  }

  const buildAdditionalDetailsPayload = () => {
    if (!account) {
      setError('Please select an account in Additional Details.')
      return null
    }
    if (!postingDate || !paymentDate) {
      setError('Posting date and payment date are required.')
      return null
    }
    return {
      modeOfPayment,
      account,
      journalRemark: journalRemark || undefined,
      referenceNo: referenceNo || undefined,
      postingDate,
      paymentDate,
    }
  }

  const handleSaveAdditionalDetails = async () => {
    if (!batch?.id || !canEditAdditionalDetails) return
    const payload = buildAdditionalDetailsPayload()
    if (!payload) return

    setSubmitting(true)
    setError(null)
    setSuccessMessage(null)
    try {
      const result = await updateBatchAdditionalDetails(batch.id, payload)
      applyLoadedBatch(result)
      setSuccessMessage('Additional details saved.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save additional details')
    } finally {
      setSubmitting(false)
    }
  }

  const handleApprove = async () => {
    if (!approvalRequestId) return
    if (batch?.id && canEditAdditionalDetails) {
      const details = buildAdditionalDetailsPayload()
      if (!details) return
      setSubmitting(true)
      setError(null)
      try {
        const updated = await updateBatchAdditionalDetails(batch.id, details)
        applyLoadedBatch(updated)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to save additional details')
        setSubmitting(false)
        return
      }
    } else {
      setSubmitting(true)
      setError(null)
    }
    try {
      await approveRequest(Number(approvalRequestId), {
        comment: comment || 'Approved',
        utrNo: needsUtr ? utrNo : undefined,
        utrDate: needsUtr && utrDate ? utrDate : undefined,
      })
      setSuccessMessage('Payment request approved.')
      navigate(ROUTES.APPROVALS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Approve failed')
    } finally {
      setSubmitting(false)
    }
  }

  const handleReject = async () => {
    if (!approvalRequestId) return
    if (!comment.trim()) {
      setError('Comment is required before rejecting.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await rejectRequest(Number(approvalRequestId), comment)
      setSuccessMessage('Payment request rejected.')
      navigate(ROUTES.APPROVALS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Reject failed')
    } finally {
      setSubmitting(false)
    }
  }

  const handleCancelBatch = async () => {
    if (!batch?.id || !batch.canCancel) return
    if (!window.confirm('Cancel this batch payment in SAP? Linked SAP documents will be cancelled.')) return
    setSubmitting(true)
    setError(null)
    setSuccessMessage(null)
    try {
      const result = await cancelStageWisePaymentBatch(batch.id)
      if (!result.success) {
        const failed = result.operations?.filter((op) => !op.success).map((op) => op.message).join(' ')
        throw new Error(failed || 'Batch cancellation failed')
      }
      setSuccessMessage('Batch payment cancelled successfully.')
      const updated = await getStageWisePaymentBatch(batch.id)
      setBatch(updated)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Batch cancellation failed')
    } finally {
      setSubmitting(false)
    }
  }

  const handleDeleteBatch = async () => {
    if (!batch?.id || !batch.canDelete) return
    if (!window.confirm('Delete this batch payment request? This cannot be undone.')) return
    setSubmitting(true)
    setError(null)
    try {
      await deleteStageWisePaymentBatch(batch.id)
      navigate(`/purchase-orders/${poDocEntry}/payments`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Batch deletion failed')
    } finally {
      setSubmitting(false)
    }
  }

  const handleDownloadBatchPdf = async () => {
    if (!batch?.id) return
    setSubmitting(true)
    setError(null)
    try {
      const blob = await downloadStageWisePaymentBatchPdf(batch.id)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `Batch Payment Requisition(${batch.id}).pdf`
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'PDF download failed')
    } finally {
      setSubmitting(false)
    }
  }

  const titleSuffix = po?.DocNum ?? po?.DocEntry ?? id
  const pageTitle = isApproval
    ? `Approve Batch Payment (${titleSuffix})`
    : isEditingExisting
      ? `Edit Batch Payment (${titleSuffix})`
      : readOnly
        ? `Batch Payment (${titleSuffix})`
        : `New Batch Payment (${titleSuffix})`

  return (
    <div className="space-y-6">
      <PageHeader
        title={pageTitle}
        description={po ? `${po.CardName ?? ''}` : ''}
        actionLabel="Back to Payments"
        actionTo={`/purchase-orders/${poDocEntry}/payments`}
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {successMessage && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">{successMessage}</div>
      )}

      {batch && (
        <div className="flex flex-wrap items-center gap-3 text-sm">
          <Badge>{batch.status}</Badge>
          {batch.approvalRequestId && (
            <span className="text-slate-600">Approval Request: {batch.approvalRequestId}</span>
          )}
          {canWithdraw && (
            <Button
              type="button"
              variant="outline"
              disabled={submitting}
              onClick={() => void handleWithdraw()}
            >
              <Undo2 className="mr-2 h-4 w-4" />
              Withdraw & Edit
            </Button>
          )}
          {batch.canCancel && !isApproval && (
            <Button
              type="button"
              variant="outline"
              disabled={submitting}
              onClick={() => void handleCancelBatch()}
            >
              <Ban className="mr-2 h-4 w-4" />
              Cancel Batch Payment
            </Button>
          )}
          {batch.stageWisePaymentId && !isApproval && (
            <Button
              type="button"
              variant="outline"
              disabled={submitting}
              onClick={() => void handleDownloadBatchPdf()}
            >
              <Download className="mr-2 h-4 w-4" />
              Download PDF
            </Button>
          )}
          {batch.canDelete && !isApproval && (
            <Button
              type="button"
              variant="outline"
              disabled={submitting}
              onClick={() => void handleDeleteBatch()}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete Batch
            </Button>
          )}
        </div>
      )}

      {po && (
        <Card>
          <CardContent className="pt-6">
            <div className="grid gap-4 text-sm md:grid-cols-2 lg:grid-cols-4">
              <div><span className="text-slate-500">Vendor:</span> <strong>{po.CardName}</strong></div>
              <div><span className="text-slate-500">Gross Total:</span> <strong>{formatAmount(po.DocTotal)}</strong></div>
              <div><span className="text-slate-500">Balance Payment:</span> <strong>{formatAmount(pageData?.balancePayment)}</strong></div>
              <div><span className="text-slate-500">PO Status:</span> <strong>{po?.DocumentStatus === 'bost_Close' ? 'Closed' : 'Open'}</strong></div>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid max-w-3xl gap-4 md:grid-cols-2">
              <Select
                label="Bank A/C (all rows)"
                options={bankOptions}
                value={sharedBank}
                onChange={setSharedBank}
                placeholder="Select bank"
                disabled={readOnly || loading}
                required={!readOnly}
                usePortal
                minHeight="min-h-[44px]"
                menuMinHeight="min-h-52"
              />
              <Select
                label="WT Code (TDS — all rows)"
                options={wtCodeOptions}
                value={sharedWtCode}
                onChange={setSharedWtCode}
                placeholder="Select WT code"
                disabled={readOnly || loading}
                clearable
                usePortal
                minHeight="min-h-[44px]"
                menuMinHeight="min-h-52"
              />
            </div>

            <div className="overflow-x-auto overflow-y-visible rounded-xl border border-slate-200">
              <table className="min-w-[1100px] w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-left text-slate-600">
                    <th className="px-3 py-2">Payment Type</th>
                    <th className="px-3 py-2">AP Invoice</th>
                    <th className="px-3 py-2 text-right">AP Invoice Balance Due</th>
                    <th className="px-3 py-2 text-right">Payable</th>
                    <th className="px-3 py-2 text-right">Net Amt</th>
                    {!readOnly && <th className="px-3 py-2 w-px whitespace-nowrap" />}
                  </tr>
                </thead>
                <tbody>
                  {displayRows.map((row, rowIndex) => {
                    const batchLine = batch?.lines[rowIndex]
                    const termIds = row.paymentTermsTypes.map(Number)
                    const requiresAp = !readOnly && po
                      ? batchRowRequiresApInvoice(po, paymentTerms, termIds)
                      : Boolean(row.apInvoiceDocEntry)
                    const apLabel = readOnly
                      ? batchLine?.apInvoiceLabel
                      : undefined
                    return (
                      <tr key={row.key} className="border-b border-slate-100 align-top">
                        <td className="px-3 py-3 min-w-[240px] align-top">
                          {readOnly ? (
                            <span>{batchLine?.paymentTermLabels?.join(', ') ?? '—'}</span>
                          ) : (
                            <Select
                              options={paymentTermOptions}
                              value={row.paymentTermsTypes[0] ?? ''}
                              onChange={(val) => void updateRow(row.key, {
                                paymentTermsTypes: val ? [val] : [],
                              })}
                              placeholder="Select payment type"
                              required
                              usePortal
                              minHeight="min-h-[44px]"
                              menuMinHeight="min-h-52"
                            />
                          )}
                        </td>
                        <td className="px-3 py-2 min-w-[220px]">
                          {readOnly ? (
                            <span>{apLabel ?? (row.apInvoiceDocEntry || '—')}</span>
                          ) : requiresAp ? (
                            <SearchableSelect
                              value={row.apInvoiceDocEntry}
                              selectedLabel={apLabel}
                              placeholder="Search AP invoice..."
                              searchPlaceholder="Doc num or reference"
                              disabled={!hasApInvoices}
                              required
                              onSearch={searchApInvoiceOptions}
                              onChange={(docEntry) => void updateRow(row.key, { apInvoiceDocEntry: docEntry })}
                              usePortal
                              minHeight="min-h-[44px]"
                              menuMinHeight="min-h-52"
                            />
                          ) : (
                            <span className="text-slate-500">—</span>
                          )}
                        </td>
                        <td className="px-3 py-2 text-right">
                          {requiresAp || readOnly ? formatAmount(row.balanceDue) : '—'}
                        </td>
                        <td className="px-3 py-2 text-right">{formatAmount(row.payable)}</td>
                        <td className="px-3 py-2 min-w-[120px]">
                          {readOnly ? (
                            <span className="block text-right">{formatAmount(Number(row.amount))}</span>
                          ) : (
                            <Input
                              type="number"
                              step="0.01"
                              min="0"
                              nonNegative
                              value={row.amount}
                              onChange={(e) => updateRow(row.key, { amount: e.target.value })}
                              required
                            />
                          )}
                        </td>
                        {!readOnly && (
                          <td className={rowActionsCellClassName}>
                            <RowActionButton
                              title="Remove row"
                              variant="danger"
                              icon={<Trash2 className={rowActionIconClassName} />}
                              onClick={() => removeRow(row.key)}
                              disabled={rows.length <= 1}
                            />
                          </td>
                        )}
                      </tr>
                    )
                  })}
                </tbody>
                <tfoot>
                  <tr className="bg-slate-50 font-medium">
                    <td colSpan={readOnly ? 4 : 4} className="px-3 py-2 text-right">Total</td>
                    <td className="px-3 py-2 text-right">{formatAmount(totalAmount)}</td>
                    {!readOnly && <td />}
                  </tr>
                </tfoot>
              </table>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50/60 p-4">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-sm font-semibold text-slate-800">Additional Details</h3>
                {canEditAdditionalDetails && readOnly && (
                  <span className="text-xs text-slate-500">
                    {batch?.hasSapOutgoingPayment
                      ? 'Payment details are locked after SAP posting'
                      : 'Editable — no pending approval required for you'}
                  </span>
                )}
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <Select
                  label="Mode of Payment"
                  options={PAYMENT_MODE_OPTIONS}
                  value={modeOfPayment}
                  onChange={setModeOfPayment}
                  disabled={sapPaymentDetailsReadOnly}
                  required={canEditAdditionalDetails}
                  usePortal
                  minHeight="min-h-[44px]"
                  menuMinHeight="min-h-52"
                />
                <Select
                  label="Account"
                  options={accountOptions}
                  value={account}
                  onChange={setAccount}
                  placeholder="Select account"
                  disabled={sapPaymentDetailsReadOnly}
                  required={canEditAdditionalDetails}
                  usePortal
                  minHeight="min-h-[44px]"
                  menuMinHeight="min-h-52"
                />
                <Input
                  label="User Remark"
                  value={journalRemark}
                  onChange={(e) => setJournalRemark(e.target.value)}
                  disabled={additionalDetailsReadOnly}
                />
                <Input
                  label="Reference No."
                  value={referenceNo}
                  onChange={(e) => setReferenceNo(e.target.value)}
                  disabled={sapPaymentDetailsReadOnly}
                />
                <Input
                  label="Posting Date"
                  type="date"
                  value={postingDate}
                  onChange={(e) => setPostingDate(e.target.value)}
                  disabled={additionalDetailsReadOnly}
                  required={canEditAdditionalDetails}
                />
                <Input
                  label="Payment Date"
                  type="date"
                  value={paymentDate}
                  onChange={(e) => setPaymentDate(e.target.value)}
                  disabled={sapPaymentDetailsReadOnly}
                  required={canEditAdditionalDetails}
                />
              </div>
              {showAdditionalDetailsSave && (
                <div className="mt-4">
                  <Button
                    type="button"
                    variant="outline"
                    disabled={submitting || loading}
                    onClick={() => void handleSaveAdditionalDetails()}
                  >
                    {submitting ? 'Saving…' : 'Save Additional Details'}
                  </Button>
                </div>
              )}
            </div>

            {!readOnly && (
              <div className="flex flex-wrap gap-3">
                <Button type="button" variant="outline" onClick={addRow}>Add Row</Button>
                {canSubmitExisting && (
                  <Button
                    type="button"
                    variant="outline"
                    disabled={submitting || loading || !batchPaymentEnabled}
                    onClick={() => void handleSaveDraft()}
                  >
                    {submitting ? 'Saving…' : 'Save Draft'}
                  </Button>
                )}
                <Button type="submit" disabled={submitting || loading || !batchPaymentEnabled}>
                  {submitting
                    ? 'Submitting…'
                    : isEditingExisting
                      ? 'Submit Again'
                      : 'Submit Batch Payment'}
                </Button>
              </div>
            )}

            {readOnly && (sharedBank || sharedWtCode) && (
              <div className="flex flex-wrap gap-6 text-sm text-slate-600">
                {sharedBank && (
                  <p>Bank: <strong>{bankLabel(sharedBank)}</strong></p>
                )}
                {sharedWtCode && (
                  <p>WT Code: <strong>{wtCodeOptions.find((o) => o.value === sharedWtCode)?.label ?? sharedWtCode}</strong></p>
                )}
              </div>
            )}
          </form>
        </CardContent>
      </Card>

      {isApproval && canAct && (
        <Card>
          <CardContent className="space-y-4 pt-6">
            <Textarea label="Comments" value={comment} onChange={(e) => setComment(e.target.value)} />
            {needsUtr && (
              <div className="grid gap-4 md:grid-cols-2">
                <Input label="UTR No" value={utrNo} onChange={(e) => setUtrNo(e.target.value)} required />
                <Input label="UTR Date" type="date" value={utrDate} onChange={(e) => setUtrDate(e.target.value)} required />
              </div>
            )}
            <div className="flex justify-end gap-3">
              <Button variant="outline" type="button" onClick={() => navigate(ROUTES.APPROVALS)}>Cancel</Button>
              <Button variant="outline" onClick={handleReject} disabled={submitting}>Reject</Button>
              <Button onClick={handleApprove} disabled={submitting}>
                {submitting ? 'Processing…' : 'Approve'}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

    </div>
  )
}
