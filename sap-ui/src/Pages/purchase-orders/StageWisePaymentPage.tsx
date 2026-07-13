import { useCallback, useEffect, useMemo, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { Ban, Download, Layers, Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { Button, Card, CardContent, Badge } from '@/Components/ui'
import { getApprovalRequest, type ApprovalRequest } from '@/Requests/approvals'
import { ROUTES } from '@/config/constants'
import {
  cancelStageWisePayment,
  deleteStageWisePayment,
  downloadStageWisePaymentPdf,
  getStageWisePaymentPageData,
  type StageWisePayment,
  type StageWisePaymentPageData,
} from '@/Requests/stageWisePayments'
import { getBatchByStageWisePaymentId, cancelStageWisePaymentBatch, deleteStageWisePaymentBatch } from '@/Requests/stageWisePaymentBatches'
import {
  formatAmount,
  isBatchPaymentAvailable,
  isPaymentTermSelectable,
  normalizeStatus,
  paymentTermLabel,
} from '@/helpers/stageWisePaymentCalculations'

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

function formatPoDate(value?: string) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleDateString(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

function recordGrossAmount(record: StageWisePayment) {
  return (record.grossAmount ?? 0) + (record.gstAmount ?? 0)
}

function summaryLabel(label: string) {
  if (label === 'Tax') return 'Taxes'
  return label
}

export function StageWisePaymentPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const poDocEntry = Number(id)
  const [pageData, setPageData] = useState<StageWisePaymentPageData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [viewApprovalRequest, setViewApprovalRequest] = useState<ApprovalRequest | null>(null)
  const [actingId, setActingId] = useState<number | null>(null)

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
  const tableRecords = pageData?.tableRecords ?? []
  const totalBasic = pageData?.totalBasic ?? 0

  const selectableTerms = useMemo(
    () => paymentTerms.filter(isPaymentTermSelectable),
    [paymentTerms],
  )

  const batchPaymentEnabled = isBatchPaymentAvailable(po, selectableTerms, pageData?.apInvoices) && !loading

  const totalGross = useMemo(
    () => tableRecords.reduce((sum, record) => sum + recordGrossAmount(record), 0),
    [tableRecords],
  )

  const isBatchPayment = (record: StageWisePayment) =>
    record.stageDesc === 'Batch AP payment'
    || record.stageDesc === 'Batch down payment'
    || (record.apInvoiceDocEntry?.includes(',') ?? false)

  const paymentTermForRecord = (record: StageWisePayment) => {
    if (record.stageDesc === 'Batch AP payment' || record.stageDesc === 'Batch down payment') {
      return record.stageDesc
    }
    const term = paymentTerms.find((t) => t.id === record.paymentTermsType)
    return term ? paymentTermLabel(term) : (record.stageDesc || '—')
  }

  const handleCancelPayment = async (record: StageWisePayment) => {
    if (!window.confirm('Cancel this payment in SAP?')) return
    setActingId(record.id)
    setError(null)
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
      setSuccessMessage('Payment cancelled.')
      await reload()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Cancel failed')
    } finally {
      setActingId(null)
    }
  }

  const handleDeletePayment = async (record: StageWisePayment) => {
    if (!window.confirm('Delete this payment request?')) return
    setActingId(record.id)
    setError(null)
    try {
      if (isBatchPayment(record)) {
        const batch = await getBatchByStageWisePaymentId(record.id)
        await deleteStageWisePaymentBatch(batch.id)
      } else {
        await deleteStageWisePayment(record.id)
      }
      setSuccessMessage('Payment request deleted.')
      await reload()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Delete failed')
    } finally {
      setActingId(null)
    }
  }

  const handleViewBatch = async (record: StageWisePayment) => {
    setActingId(record.id)
    setError(null)
    try {
      const batch = await getBatchByStageWisePaymentId(record.id)
      navigate(`/purchase-orders/${poDocEntry}/payments/batch/${batch.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load batch payment')
    } finally {
      setActingId(null)
    }
  }

  const handleDownload = async (record: StageWisePayment) => {
    setActingId(record.id)
    setError(null)
    try {
      const blob = await downloadStageWisePaymentPdf(record.id, poDocEntry)
      triggerDownload(
        blob,
        `Payment Requisition(${record.apDownPaymentInvoiceEntryNumber ?? record.id}).pdf`,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to download PDF')
    } finally {
      setActingId(null)
    }
  }

  const openApprovalRequest = async (requestId: string) => {
    try {
      setViewApprovalRequest(await getApprovalRequest(Number(requestId)))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load approval request')
    }
  }

  const poDate = po?.PostingDate ?? po?.DocDate

  return (
    <div className="space-y-6">
      <PageHeader
        title={po ? `Payment Request (${po.DocNum ?? po.DocEntry ?? id})` : 'Payment Request'}
        description="Stage-wise payments for this purchase order"
        action={(
          <Link to={ROUTES.PURCHASE_ORDERS}>
            <Button variant="outline">Back to PO List</Button>
          </Link>
        )}
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      {successMessage && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">{successMessage}</div>
      )}

      <Card>
        <CardContent className="space-y-6 pt-6">
          <div className="grid gap-3 text-sm md:grid-cols-2 xl:grid-cols-3">
            <div>
              <span className="text-slate-500">Vendor Details:</span>{' '}
              <strong>
                {po ? `${po.CardCode ?? '—'} - ${po.CardName ?? '—'}` : (loading ? 'Loading…' : '—')}
              </strong>
            </div>
            <div>
              <span className="text-slate-500">PO Details:</span>{' '}
              <strong>
                {po ? `${po.DocNum ?? po.DocEntry ?? '—'}` : '—'}
              </strong>
              <span className="ml-3 text-slate-500">PO Date:</span>{' '}
              <strong>{formatPoDate(poDate)}</strong>
            </div>
            <div>
              <span className="text-slate-500">Project Details:</span>{' '}
              <strong>
                {po
                  ? `${po.Project ?? '—'}${pageData?.projectName ? ` - ${pageData.projectName}` : ''}`
                  : '—'}
              </strong>
            </div>
          </div>

          <div>
            <h2 className="mb-3 text-base font-semibold text-slate-900">PO Summary</h2>
            <div className="overflow-x-auto rounded-xl border border-slate-200">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-left text-slate-600">
                    <th className="px-4 py-3 font-medium" />
                    <th className="px-4 py-3 text-right font-medium">P.O. Value</th>
                    <th className="px-4 py-3 text-right font-medium">Requested</th>
                    <th className="px-4 py-3 text-right font-medium">Paid</th>
                    <th className="px-4 py-3 text-right font-medium">Balance</th>
                  </tr>
                </thead>
                <tbody>
                  {(pageData?.paymentSummary.length ?? 0) > 0 ? (
                    pageData?.paymentSummary.map((row) => (
                      <tr
                        key={row.label}
                        className={`border-b border-slate-100 ${row.label === 'Total' ? 'bg-slate-50 font-semibold' : ''}`}
                      >
                        <td className="px-4 py-3 font-medium text-slate-800">{summaryLabel(row.label)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(row.poValue)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(row.requested)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(row.paid)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(row.balance)}</td>
                      </tr>
                    ))
                  ) : (
                    <>
                      <tr className="border-b border-slate-100">
                        <td className="px-4 py-3 font-medium">Basic</td>
                        <td className="px-4 py-3 text-right">{formatAmount(totalBasic)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(totalBasic)}</td>
                      </tr>
                      <tr className="border-b border-slate-100">
                        <td className="px-4 py-3 font-medium">Taxes</td>
                        <td className="px-4 py-3 text-right">{formatAmount(po?.VatSum)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(po?.VatSum)}</td>
                      </tr>
                      <tr className="bg-slate-50 font-semibold">
                        <td className="px-4 py-3">Total</td>
                        <td className="px-4 py-3 text-right">{formatAmount(po?.DocTotal)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(0)}</td>
                        <td className="px-4 py-3 text-right">{formatAmount(po?.DocTotal)}</td>
                      </tr>
                    </>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-base font-semibold text-slate-900">Detailed</h2>
            <span className="text-sm text-slate-500">List of Request</span>
          </div>

          <div className="overflow-x-auto rounded-xl border border-slate-200">
            <table className="min-w-[900px] w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-left text-slate-600">
                  <th className="px-4 py-3 font-medium">Request ID</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">SAP Document No.</th>
                  <th className="px-4 py-3 font-medium">Payment Term</th>
                  <th className="px-4 py-3 text-right font-medium">Gross Amount</th>
                  <th className="px-4 py-3 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-slate-500">Loading payment requests…</td>
                  </tr>
                ) : tableRecords.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-slate-500">No payment requests yet.</td>
                  </tr>
                ) : (
                  tableRecords.map((record) => {
                    const requestIds = (record.approvalRequestId ?? '')
                      .split(',')
                      .map((x) => x.trim())
                      .filter(Boolean)
                    const busy = actingId === record.id
                    return (
                      <tr key={record.id} className="border-b border-slate-100 align-middle">
                        <td className="px-4 py-3">
                          {requestIds.length === 0 ? (
                            <span className="text-slate-400">—</span>
                          ) : (
                            <div className="flex flex-wrap gap-1">
                              {requestIds.map((requestId) => (
                                <button
                                  key={requestId}
                                  type="button"
                                  className="text-primary-600 underline"
                                  onClick={() => void openApprovalRequest(requestId)}
                                >
                                  {requestId}
                                </button>
                              ))}
                            </div>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <Badge>{normalizeStatus(record.status)}</Badge>
                        </td>
                        <td className="px-4 py-3">{record.apDownPaymentInvoiceEntryNumber || '—'}</td>
                        <td className="px-4 py-3">{paymentTermForRecord(record)}</td>
                        <td className="px-4 py-3 text-right font-medium">{formatAmount(recordGrossAmount(record))}</td>
                        <td className="px-4 py-3">
                          <RowActions>
                            {isBatchPayment(record) && (
                              <RowActionButton
                                title="Open batch"
                                disabled={busy}
                                icon={<Layers className={rowActionIconClassName} />}
                                onClick={() => void handleViewBatch(record)}
                              />
                            )}
                            <RowActionButton
                              title="Download PDF"
                              disabled={busy}
                              icon={<Download className={rowActionIconClassName} />}
                              onClick={() => void handleDownload(record)}
                            />
                            <RowActionButton
                              title="Delete payment"
                              variant="danger"
                              disabled={busy}
                              icon={<Trash2 className={rowActionIconClassName} />}
                              onClick={() => void handleDeletePayment(record)}
                            />
                            <RowActionButton
                              title="Cancel payment"
                              disabled={busy}
                              icon={<Ban className={rowActionIconClassName} />}
                              onClick={() => void handleCancelPayment(record)}
                            />
                          </RowActions>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
              <tfoot>
                <tr className="bg-slate-50 font-semibold">
                  <td className="px-4 py-3" colSpan={4}>Total Gross</td>
                  <td className="px-4 py-3 text-right">{formatAmount(totalGross)}</td>
                  <td className="px-4 py-3" />
                </tr>
              </tfoot>
            </table>
          </div>

          <div className="mt-6 flex flex-wrap items-center gap-3">
            <Button
              disabled={!batchPaymentEnabled}
              title={batchPaymentEnabled ? undefined : 'Configure payment types for this purchase order to create payments'}
              onClick={() => navigate(`/purchase-orders/${poDocEntry}/payments/batch`)}
            >
              <Plus className="mr-2 h-4 w-4" />
              Create New
            </Button>
            {!batchPaymentEnabled && po && (
              <p className="text-sm text-amber-700">
                Payment creation is unavailable until payment types are configured for this purchase order.
              </p>
            )}
          </div>
        </CardContent>
      </Card>

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
