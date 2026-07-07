import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { Ban, Download, Layers, Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { Button, Card, CardContent, Input, Badge } from '@/Components/ui'
import { getApprovalRequest, type ApprovalRequest } from '@/Requests/approvals'
import { ROUTES } from '@/config/constants'
import {
  cancelStageWisePayment,
  createStageWisePayment,
  deleteStageWisePayment,
  downloadStageWisePaymentPdf,
  getStageWisePaymentPageData,
  type StageWisePayment,
  type StageWisePaymentPageData,
} from '@/Requests/stageWisePayments'
import { getBatchByStageWisePaymentId, cancelStageWisePaymentBatch, deleteStageWisePaymentBatch } from '@/Requests/stageWisePaymentBatches'
import {
  filterSinglePaymentTerms,
  formatAmount,
  isBatchPaymentAvailable,
  isPaymentTermSelectable,
  isPoClosed,
  normalizeStatus,
  paymentTermLabel,
  requiresBatchPayment,
  resolveDisplayPayable,
} from '@/helpers/stageWisePaymentCalculations'

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

export function StageWisePaymentPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const poDocEntry = Number(id)
  const [pageData, setPageData] = useState<StageWisePaymentPageData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [viewApprovalRequest, setViewApprovalRequest] = useState<ApprovalRequest | null>(null)
  const [form, setForm] = useState({
    paymentTermId: '',
    amount: '',
    bank: '',
    wtCode: '',
  })

  const reload = useCallback(async () => {
    const data = await getStageWisePaymentPageData(poDocEntry)
    setPageData(data)
  }, [poDocEntry])

  useEffect(() => {
    reload()
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load payment data'))
      .finally(() => setLoading(false))
  }, [reload])

  const po = pageData?.purchaseOrder
  const paymentTerms = pageData?.paymentTerms ?? []
  const activeRecords = pageData?.activeRecords ?? []
  const tableRecords = pageData?.tableRecords ?? []
  const totalBasic = pageData?.totalBasic ?? 0

  const selectableTerms = useMemo(
    () => paymentTerms.filter(isPaymentTermSelectable),
    [paymentTerms],
  )

  const singlePaymentTerms = useMemo(
    () => filterSinglePaymentTerms(po, selectableTerms),
    [po, selectableTerms],
  )

  const singlePaymentAvailable = singlePaymentTerms.length > 0

  const selectedTerm = singlePaymentTerms.find((t) => String(t.id) === form.paymentTermId)

  const batchPaymentEnabled = isBatchPaymentAvailable(po, selectableTerms, pageData?.apInvoices) && !loading
  const poClosed = isPoClosed(po)

  const isBatchPayment = (record: StageWisePayment) =>
    record.stageDesc === 'Batch AP payment'
    || record.stageDesc === 'Batch down payment'
    || (record.apInvoiceDocEntry?.includes(',') ?? false)

  const handleCancelPayment = async (record: StageWisePayment) => {
    if (!window.confirm('Cancel this payment in SAP?')) return
    try {
      if (isBatchPayment(record)) {
        const batch = await getBatchByStageWisePaymentId(record.id)
        const result = await cancelStageWisePaymentBatch(batch.id)
        if (!result.success) {
          const failed = result.operations?.filter((op) => !op.success).map((op) => op.message).join(' ')
          throw new Error(failed || 'Batch cancellation failed')
        }
      } else {
        await cancelStageWisePayment(record.id)
      }
      await reload()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Cancel failed')
    }
  }

  const handleDeletePayment = async (record: StageWisePayment) => {
    if (!window.confirm('Delete this payment request?')) return
    try {
      if (isBatchPayment(record)) {
        const batch = await getBatchByStageWisePaymentId(record.id)
        await deleteStageWisePaymentBatch(batch.id)
      } else {
        await deleteStageWisePayment(record.id)
      }
      await reload()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Delete failed')
    }
  }

  const handleViewBatch = async (record: StageWisePayment) => {
    try {
      const batch = await getBatchByStageWisePaymentId(record.id)
      navigate(`/purchase-orders/${poDocEntry}/payments/batch/${batch.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load batch payment')
    }
  }

  const payable = po && selectedTerm
    ? resolveDisplayPayable(
      po,
      paymentTerms,
      activeRecords,
      selectedTerm,
      '',
      undefined,
      totalBasic,
    )
    : 0

  const handleTermChange = (paymentTermId: string) => {
    setForm((prev) => ({ ...prev, paymentTermId }))
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedTerm || !po) return

    if (requiresBatchPayment(po, selectedTerm)) {
      setError('AP invoice payments must be created using batch payment.')
      navigate(`/purchase-orders/${poDocEntry}/payments/batch`)
      return
    }

    const amount = Number(form.amount)
    if (amount > payable) {
      setError(`Amount cannot exceed payable of ${formatAmount(payable)}`)
      return
    }

    if (!form.bank) {
      setError('Please select bank')
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const result = await createStageWisePayment({
        poDocEntry,
        docNumber: po.DocNum,
        paymentTermsType: selectedTerm.id,
        stageDesc: selectedTerm.desc,
        bank: form.bank,
        selectedPaymentTermsUdf: selectedTerm,
        downPaymentAmount: amount,
        wtCode: form.wtCode || undefined,
        desc: selectedTerm.desc,
      }) as { message?: string }
      await reload()
      setSuccessMessage(result?.message ?? 'Payment request created.')
      setForm({ paymentTermId: '', amount: '', bank: '', wtCode: '' })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create payment request')
    } finally {
      setSubmitting(false)
    }
  }

  const handleDownload = async (record: StageWisePayment) => {
    try {
      const blob = await downloadStageWisePaymentPdf(record.id, poDocEntry)
      triggerDownload(
        blob,
        `Payment Requisition(${record.apDownPaymentInvoiceEntryNumber ?? record.id}).pdf`,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to download PDF')
    }
  }

  const bankLabel = (key?: string) => {
    if (!key) return ''
    return pageData?.bankLabels[key] ?? key
  }

  const columns: SapColumn<StageWisePayment>[] = [
    { key: 'id', header: 'ID', accessor: (r) => r.id },
    {
      key: 'approvalRequestId',
      header: 'Request ID',
      render: (r) => {
        const ids = (r.approvalRequestId ?? '').split(',').map((x) => x.trim()).filter(Boolean)
        if (ids.length === 0) return '—'
        return (
          <div className="flex flex-wrap gap-1">
            {ids.map((requestId) => (
              <button
                key={requestId}
                type="button"
                className="text-primary-600 underline"
                onClick={async () => {
                  try {
                    setViewApprovalRequest(await getApprovalRequest(Number(requestId)))
                  } catch (err) {
                    setError(err instanceof Error ? err.message : 'Failed to load approval request')
                  }
                }}
              >
                {requestId}
              </button>
            ))}
          </div>
        )
      },
    },
    { key: 'apDownPaymentInvoiceEntryNumber', header: 'Outgoing Payment', accessor: (r) => r.apDownPaymentInvoiceEntryNumber },
    { key: 'status', header: 'Status', render: (r) => <Badge>{normalizeStatus(r.status)}</Badge> },
    { key: 'apInvoiceDocEntry', header: 'AP Invoice Doc Entry', accessor: (r) => r.apInvoiceDocEntry },
    { key: 'bank', header: 'Bank', accessor: (r) => bankLabel(r.bank) },
    {
      key: 'paymentTerms',
      header: 'Payment Terms',
      accessor: (r) => paymentTermLabel(paymentTerms.find((t) => t.id === r.paymentTermsType) ?? {}),
    },
    {
      key: 'grossTotal',
      header: 'Gross Amount',
      accessor: (r) => formatAmount((r.grossAmount ?? 0) + (r.gstAmount ?? 0)),
    },
    { key: 'grossAmount', header: 'Basic Amount', accessor: (r) => formatAmount(r.grossAmount) },
    { key: 'tds', header: 'TDS', accessor: (r) => formatAmount(r.tds) },
    { key: 'gstAmount', header: 'GST', accessor: (r) => formatAmount(r.gstAmount) },
    {
      key: 'netAmount',
      header: 'Net Amount',
      accessor: (r) => formatAmount((r.grossAmount ?? 0) - (r.tds ?? 0)),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={po ? `Payment Request (${po.DocNum ?? po.DocEntry ?? id})` : 'Payment Request'}
        description={po ? `${po.CardName ?? ''}` : ''}
        action={(
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              disabled={!batchPaymentEnabled}
              title={batchPaymentEnabled ? undefined : 'Batch payment requires AP invoices linked to this purchase order'}
              onClick={() => navigate(`/purchase-orders/${poDocEntry}/payments/batch`)}
            >
              Batch Payment
            </Button>
            <Link to={ROUTES.PURCHASE_ORDERS}>
              <Button variant="outline">Back to PO List</Button>
            </Link>
          </div>
        )}
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {successMessage && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">{successMessage}</div>
      )}

      {po && (
        <Card>
          <CardContent className="pt-6">
            <div className="grid gap-6 lg:grid-cols-2">
              <div className="space-y-2 text-sm">
                <div><span className="text-slate-500">Vendor Name:</span> <strong>{po.CardName}</strong></div>
                <div>
                  <span className="text-slate-500">Project Details:</span>{' '}
                  <strong>{po.Project}{pageData?.projectName ? ` - ${pageData.projectName}` : ''}</strong>
                </div>
                <div><span className="text-slate-500">Basic Amount:</span> <strong>{formatAmount(totalBasic)}</strong></div>
                <div><span className="text-slate-500">Tax Amount:</span> <strong>{formatAmount(po.VatSum)}</strong></div>
                <div><span className="text-slate-500">Gross Total:</span> <strong>{formatAmount(po.DocTotal)}</strong></div>
                <div><span className="text-slate-500">Balance Payment:</span> <strong>{formatAmount(pageData?.balancePayment)}</strong></div>
              </div>

              <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                <h3 className="mb-3 text-base font-semibold text-primary-700">Payment Summary</h3>
                {(pageData?.paymentSummary.length ?? 0) > 0 ? (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-200 text-left text-slate-500">
                          <th className="py-2 pr-3">Type</th>
                          <th className="py-2 pr-3 text-right">PO Value</th>
                          <th className="py-2 pr-3 text-right">Requested</th>
                          <th className="py-2 pr-3 text-right">Paid</th>
                          <th className="py-2 text-right">Balance</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pageData?.paymentSummary.map((row) => (
                          <tr key={row.label} className="border-b border-slate-100">
                            <td className="py-2 pr-3 font-medium">{row.label}</td>
                            <td className="py-2 pr-3 text-right">{formatAmount(row.poValue)}</td>
                            <td className="py-2 pr-3 text-right">{formatAmount(row.requested - row.paid)}</td>
                            <td className="py-2 pr-3 text-right">{formatAmount(row.paid)}</td>
                            <td className="py-2 text-right">{formatAmount(row.balance)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="text-sm text-slate-500">No payments summary available.</p>
                )}
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <div>
        <h2 className="mb-3 text-lg font-semibold">Payment Requests</h2>
        <SapDataGrid
          loading={loading}
          data={tableRecords}
          getRowKey={(r) => r.id}
          columns={columns}
          actions={(row) => (
            <RowActions>
              {isBatchPayment(row) && (
                <RowActionButton
                  title="View batch"
                  icon={<Layers className={rowActionIconClassName} />}
                  onClick={() => void handleViewBatch(row)}
                />
              )}
              <RowActionButton
                title="Download PDF"
                icon={<Download className={rowActionIconClassName} />}
                onClick={() => handleDownload(row)}
              />
              <RowActionButton
                title="Delete payment"
                variant="danger"
                icon={<Trash2 className={rowActionIconClassName} />}
                onClick={() => void handleDeletePayment(row)}
              />
              <RowActionButton
                title="Cancel payment"
                icon={<Ban className={rowActionIconClassName} />}
                onClick={() => void handleCancelPayment(row)}
              />
            </RowActions>
          )}
        />
      </div>

      <Card>
        <CardContent className="flex flex-col gap-4 pt-6 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Batch Payment</h2>
            <p className="mt-1 text-sm text-slate-500">
              Create AP invoice payments (vendor outgoing) or down payments (advance/stage) in one batch.
              Invoice and Retention terms require an AP invoice; other terms on an open PO do not.
            </p>
            {poClosed && (
              <p className="mt-2 text-sm text-slate-600">
                This purchase order is closed. AP invoice payments require a linked AP invoice.
              </p>
            )}
            {!batchPaymentEnabled && po && (
              <p className="mt-2 text-sm text-amber-700">
                Batch payment is available when payment types are configured for this purchase order.
              </p>
            )}
          </div>
          <Button
            disabled={!batchPaymentEnabled}
            onClick={() => navigate(`/purchase-orders/${poDocEntry}/payments/batch`)}
          >
            Open Batch Payment
          </Button>
        </CardContent>
      </Card>

      {singlePaymentAvailable ? (
        <Card>
          <CardContent className="pt-6">
            <h2 className="mb-1 text-lg font-semibold">Down Payment Request</h2>
            <p className="mb-4 text-sm text-slate-500">
              For advance / stage down payments only. AP invoice payments must use batch above.
            </p>
            <form onSubmit={handleSubmit} className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              <div>
                <label className="mb-1 block text-sm font-medium">Payment Type</label>
                <select
                  className="w-full rounded-lg border px-3 py-2"
                  value={form.paymentTermId}
                  onChange={(e) => handleTermChange(e.target.value)}
                  required
                >
                  <option value="">Select payment type</option>
                  {singlePaymentTerms.map((t) => (
                    <option key={t.id} value={t.id}>{paymentTermLabel(t)}</option>
                  ))}
                </select>
              </div>

              <Input label="Payable" value={formatAmount(payable)} readOnly disabled />

              <div>
                <label className="mb-1 block text-sm font-medium">WT Code</label>
                <select
                  className="w-full rounded-lg border px-3 py-2"
                  value={form.wtCode}
                  onChange={(e) => setForm({ ...form, wtCode: e.target.value })}
                >
                  <option value="">Select WT code</option>
                  {pageData?.withholdingTaxCodes.map((wt) => (
                    <option key={wt.wtCode} value={wt.wtCode}>
                      {wt.wtName ?? wt.wtCode}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium">Bank A/C</label>
                <select
                  className="w-full rounded-lg border px-3 py-2"
                  value={form.bank}
                  onChange={(e) => setForm({ ...form, bank: e.target.value })}
                  required
                >
                  <option value="">Select bank</option>
                  {pageData?.banks.map((b) => (
                    <option key={b.key} value={b.key}>{b.value}</option>
                  ))}
                </select>
              </div>

              <Input
                label="Down Payment Amount"
                type="number"
                step="0.01"
                min="0"
                nonNegative
                value={form.amount}
                onChange={(e) => setForm({ ...form, amount: e.target.value })}
                required
              />

              <div className="flex items-end">
                <Button type="submit" disabled={submitting || loading}>
                  {submitting ? 'Creating…' : 'Create'}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : po && (
        <Card>
          <CardContent className="pt-6">
            <p className="text-sm text-slate-600">
              {poClosed
                ? 'This purchase order is closed. All payments must be created via AP Invoice Payment (Batch) above.'
                : 'Invoice and retention payment types are only available through batch payment.'}
            </p>
          </CardContent>
        </Card>
      )}

      <RequestViewDialog
        request={viewApprovalRequest}
        readOnly
        onClose={() => setViewApprovalRequest(null)}
        onCompleted={() => {
          setViewApprovalRequest(null)
          void reload()
        }}
      />
    </div>
  )
}
