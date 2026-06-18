import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { RequestViewDialog } from '@/Components/approvals/RequestViewDialog'
import { Button, Card, CardContent, Input, Badge, SearchableSelect } from '@/Components/ui'
import { getApprovalRequest, type ApprovalRequest } from '@/Requests/approvals'
import type { SelectOption } from '@/types'
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
import {
  formatAmount,
  isPaymentTermSelectable,
  normalizeStatus,
  paymentTermLabel,
  resolveDisplayPayable,
  getApInvoiceBalanceDue,
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
    apInvoiceDocEntry: '',
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

  const selectedTerm = selectableTerms.find((t) => String(t.id) === form.paymentTermId)
  const selectedApInvoice = pageData?.apInvoices.find(
    (inv) => String(inv.DocEntry) === form.apInvoiceDocEntry,
  )

  const showApInvoices = po?.DocumentStatus === 'bost_Close'

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
    || selectedTerm?.type === 'Invoice'
    || selectedTerm?.type === 'Retention'

  const payable = po && selectedTerm
    ? resolveDisplayPayable(
      po,
      paymentTerms,
      activeRecords,
      selectedTerm,
      form.apInvoiceDocEntry,
      selectedApInvoice,
      totalBasic,
    )
    : 0

  const apInvoiceBalance = po && selectedTerm
    ? getApInvoiceBalanceDue(po, selectedTerm, selectedApInvoice, activeRecords, form.apInvoiceDocEntry)
    : 0

  const handleTermChange = (paymentTermId: string) => {
    const term = selectableTerms.find((t) => String(t.id) === paymentTermId)
    const keepApInvoice = term?.type === 'Invoice' || term?.type === 'Retention'
    setForm((prev) => ({
      ...prev,
      paymentTermId,
      apInvoiceDocEntry: keepApInvoice ? prev.apInvoiceDocEntry : '',
    }))
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedTerm || !po) return

    if ((selectedTerm.type === 'Invoice' || selectedTerm.type === 'Retention') && !form.apInvoiceDocEntry) {
      setError('AP Invoice is mandatory for selected payment term')
      return
    }

    const amount = Number(form.amount)
    if ((selectedTerm.type === 'Invoice' || selectedTerm.type === 'Retention') && amount > apInvoiceBalance) {
      setError(`Payable must not exceed AP Invoice Balance Due of ${formatAmount(apInvoiceBalance)}`)
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
        apInvoiceDocEntry: form.apInvoiceDocEntry || undefined,
        selectedPaymentTermsUdf: selectedTerm,
        downPaymentAmount: amount,
        wtCode: form.wtCode || undefined,
        desc: selectedTerm.desc,
      }) as { message?: string }
      await reload()
      setSuccessMessage(result?.message ?? 'Payment request created.')
      setForm({ paymentTermId: '', amount: '', bank: '', wtCode: '', apInvoiceDocEntry: '' })
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
        title={po ? `Payment Request (${po.DocNum})` : 'Payment Request'}
        description={po ? `${po.CardName ?? ''}` : ''}
        actionLabel="Back to PO List"
        actionTo={ROUTES.PURCHASE_ORDERS}
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
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="outline" onClick={() => handleDownload(row)}>Download</Button>
              <Button size="sm" variant="outline" onClick={() => deleteStageWisePayment(row.id).then(reload).catch((e) => setError(e.message))}>Delete</Button>
              <Button size="sm" variant="outline" onClick={() => cancelStageWisePayment(row.id).then(reload).catch((e) => setError(e.message))}>Cancel</Button>
            </div>
          )}
        />
      </div>

      <Card>
        <CardContent className="pt-6">
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
                {selectableTerms.map((t) => (
                  <option key={t.id} value={t.id}>{paymentTermLabel(t)}</option>
                ))}
              </select>
            </div>

            <SearchableSelect
              label="AP Invoice"
              value={form.apInvoiceDocEntry}
              selectedLabel={
                selectedApInvoice
                  ? `${selectedApInvoice.DocNum ?? ''}:${selectedApInvoice.NumAtCard ?? ''}`
                  : undefined
              }
              placeholder="Search AP invoice..."
              searchPlaceholder="Search by doc num or reference"
              disabled={!showApInvoices}
              required={selectedTerm?.type === 'Invoice' || selectedTerm?.type === 'Retention'}
              onSearch={searchApInvoiceOptions}
              onChange={(docEntry) => setForm({ ...form, apInvoiceDocEntry: docEntry })}
            />

            <Input
              label="AP Invoice Balance"
              value={formatAmount(showApInvoices ? apInvoiceBalance : 0)}
              readOnly
              disabled
            />

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
              label="DownPayment Amount"
              type="number"
              step="0.01"
              min="0"
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
