import { useEffect, useState } from 'react'
import { Button, Input, Modal, Textarea } from '@/Components/ui'
import {
  approveRequest,
  getApprovalPaymentContext,
  getApprovalRequest,
  rejectRequest,
  type ApprovalPaymentContext,
  type ApprovalRequest,
} from '@/Requests/approvals'
import { RequestBodyViewer } from '@/Components/approvals/RequestBodyViewer'
import { StageWisePaymentSummaryDialog } from '@/Components/approvals/StageWisePaymentSummaryDialog'
import { canActOnRequest, parseRequestBody, requiresUtrOnApprove } from '@/helpers/approvalUtils'

interface RequestViewDialogProps {
  request: ApprovalRequest | null
  readOnly?: boolean
  onClose: () => void
  onCompleted: () => void
}

function InfoRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex justify-between gap-4 border-b border-dashed border-slate-200 py-2 last:border-0">
      <span className="min-w-[140px] text-sm font-semibold text-slate-500">{label}</span>
      <span className="text-right text-sm font-medium text-slate-900">{value ?? '—'}</span>
    </div>
  )
}

export function RequestViewDialog({ request, readOnly = false, onClose, onCompleted }: RequestViewDialogProps) {
  const [detail, setDetail] = useState<ApprovalRequest | null>(request)
  const [paymentContext, setPaymentContext] = useState<ApprovalPaymentContext | null>(null)
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [comment, setComment] = useState('')
  const [utrNo, setUtrNo] = useState('')
  const [utrDate, setUtrDate] = useState('')
  const [showSummary, setShowSummary] = useState(false)

  useEffect(() => {
    if (!request) {
      setDetail(null)
      setPaymentContext(null)
      return
    }

    setLoading(true)
    setError(null)
    setComment('')
    setUtrNo('')
    setUtrDate('')

    void (async () => {
      try {
        const fresh = await getApprovalRequest(request.id)
        setDetail(fresh)
        if (fresh.documentType === 'Payments') {
          const ctx = await getApprovalPaymentContext(request.id)
          setPaymentContext(ctx)
          setUtrNo(ctx.utrNo ?? '')
          setUtrDate(ctx.utrDate ? new Date(ctx.utrDate).toISOString().slice(0, 10) : '')
        } else {
          setPaymentContext(null)
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load request')
        setDetail(request)
      } finally {
        setLoading(false)
      }
    })()
  }, [request])

  if (!request || !detail) return null

  const body = parseRequestBody(detail.requestBody)
  const showActions = canActOnRequest(detail, readOnly)
  const needsUtr = requiresUtrOnApprove(detail)

  const handleApprove = async () => {
    setSubmitting(true)
    setError(null)
    try {
      await approveRequest(detail.id, {
        comment: comment || 'Approved',
        utrNo: needsUtr ? utrNo : undefined,
        utrDate: needsUtr && utrDate ? utrDate : undefined,
      })
      onCompleted()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Approve failed')
    } finally {
      setSubmitting(false)
    }
  }

  const handleReject = async () => {
    if (!comment.trim()) {
      setError('Comment is required before rejecting the request.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await rejectRequest(detail.id, comment)
      onCompleted()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Reject failed')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <Modal
        isOpen={!!request}
        onClose={onClose}
        title="Request Details"
        size="full"
        className="max-h-[90vh] overflow-y-auto"
      >
        {loading ? (
          <div className="py-8 text-center text-slate-500">Loading...</div>
        ) : (
          <div className="space-y-6">
            {error && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
            )}

            <div className="grid gap-3 text-sm md:grid-cols-2 lg:grid-cols-3">
              <div><span className="text-slate-500">Request ID:</span> <strong>{detail.id}</strong></div>
              <div><span className="text-slate-500">Requester:</span> <strong>{detail.requesterUser?.fullName ?? detail.requesterUser?.userName}</strong></div>
              <div><span className="text-slate-500">Request Date:</span> <strong>{new Date(detail.createdAt).toLocaleDateString()}</strong></div>
              <div><span className="text-slate-500">Module:</span> <strong>{detail.documentType}</strong></div>
              <div><span className="text-slate-500">Action:</span> <strong>{detail.action ?? 'Create'}</strong></div>
              <div><span className="text-slate-500">Status:</span> <strong>{detail.overallStatus}</strong></div>
              {detail.supportingData && (
                <div><span className="text-slate-500">Supporting Data:</span> <strong>{detail.supportingData}</strong></div>
              )}
              {detail.failureReason && (
                <div className="md:col-span-2 text-red-600"><span className="text-slate-500">Failure:</span> <strong>{detail.failureReason}</strong></div>
              )}
            </div>

            <div>
              <h3 className="mb-3 text-sm font-semibold text-slate-700">Request Body</h3>
              {detail.documentType === 'Payments' && paymentContext ? (
                <div className="space-y-4 rounded-xl bg-slate-50 p-4">
                  <Button size="sm" variant="outline" onClick={() => setShowSummary(true)}>View Payment Summary</Button>
                  <div className="grid gap-4 lg:grid-cols-2">
                    <div className="rounded-xl border bg-white p-4">
                      <h4 className="mb-3 font-semibold text-slate-800">Vendor Details</h4>
                      <InfoRow label="Vendor Name" value={paymentContext.vendorDisplay} />
                      <InfoRow label="PO Details" value={paymentContext.poDetails} />
                      <InfoRow label="Project" value={paymentContext.projectName} />
                    </div>
                    <div className="rounded-xl border bg-white p-4">
                      <h4 className="mb-3 font-semibold text-slate-800">Transfer Details</h4>
                      <InfoRow label="Transfer Amount" value={paymentContext.transferAmount != null ? `₹ ${paymentContext.transferAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}` : undefined} />
                      <InfoRow label="Bank" value={paymentContext.bankAccount} />
                      <InfoRow label="Branch" value={paymentContext.branch} />
                    </div>
                  </div>
                  <div className="rounded-xl border bg-white p-4">
                    <h4 className="mb-3 font-semibold text-slate-800">Approval Timeline</h4>
                    {paymentContext.previousApprovals.length === 0 ? (
                      <p className="text-sm text-slate-500">No approvals completed yet.</p>
                    ) : (
                      <div className="space-y-3">
                        {paymentContext.previousApprovals.map((item, idx) => (
                          <div key={idx} className="rounded-lg border-l-4 border-green-600 bg-slate-50 p-3">
                            <InfoRow label="Approved By" value={item.approverName} />
                            <InfoRow label="Date & Time" value={item.actionDate ? new Date(item.actionDate).toLocaleString() : undefined} />
                            {item.comment && <InfoRow label="Comments" value={item.comment} />}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              ) : body ? (
                <RequestBodyViewer data={body} />
              ) : (
                <pre className="max-h-48 overflow-auto rounded bg-slate-100 p-3 text-xs">{detail.requestBody}</pre>
              )}
            </div>

            {showActions && (
              <div className="space-y-4 border-t pt-4">
                <Textarea label="Comments" value={comment} onChange={(e) => setComment(e.target.value)} />
                {needsUtr && (
                  <div className="grid gap-4 md:grid-cols-2">
                    <Input label="UTR No" value={utrNo} onChange={(e) => setUtrNo(e.target.value)} required />
                    <Input label="UTR Date" type="date" value={utrDate} onChange={(e) => setUtrDate(e.target.value)} required />
                  </div>
                )}
                <div className="flex justify-end gap-3">
                  <Button variant="outline" onClick={onClose}>Close</Button>
                  <Button variant="outline" onClick={handleReject} isLoading={submitting}>Reject</Button>
                  <Button onClick={handleApprove} isLoading={submitting}>Approve</Button>
                </div>
              </div>
            )}

            {!showActions && (
              <div className="flex justify-end border-t pt-4">
                <Button variant="outline" onClick={onClose}>Close</Button>
              </div>
            )}
          </div>
        )}
      </Modal>

      <StageWisePaymentSummaryDialog
        isOpen={showSummary}
        onClose={() => setShowSummary(false)}
        rows={paymentContext?.stageWisePayments ?? []}
      />
    </>
  )
}
