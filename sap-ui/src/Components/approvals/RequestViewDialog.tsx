import { useEffect, useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { Badge, Button, Input, Modal, Textarea } from '@/Components/ui'
import {
  approveRequest,
  getApprovalPaymentContext,
  getApprovalRequest,
  rejectRequest,
  type ApprovalPaymentContext,
  type ApprovalRequest,
} from '@/Requests/approvals'
import { ApprovalTimeline } from '@/Components/approvals/ApprovalTimeline'
import { RequestBodyViewer } from '@/Components/approvals/RequestBodyViewer'
import { StageWisePaymentSummaryDialog } from '@/Components/approvals/StageWisePaymentSummaryDialog'
import {
  canActOnRequest,
  formatDocumentType,
  getApprovalStatusBadgeVariant,
  parseRequestBody,
  requiresPaymentFinalizationDetails,
} from '@/helpers/approvalUtils'

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
  const needsPaymentDetails = requiresPaymentFinalizationDetails(detail)

  const handleApprove = async () => {
    if (needsPaymentDetails && (!utrNo.trim() || !utrDate || !comment.trim())) {
      setError('Payment date, reference number, and user remarks are all required to finalize this payment approval.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await approveRequest(detail.id, {
        comment: comment || 'Approved',
        utrNo: needsPaymentDetails ? utrNo : undefined,
        utrDate: needsPaymentDetails && utrDate ? utrDate : undefined,
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
        title={`Request #${detail.id} — ${formatDocumentType(detail.documentType)}`}
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

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant={getApprovalStatusBadgeVariant(detail.overallStatus)}>{detail.overallStatus}</Badge>
                <Badge variant="outline">{detail.action ?? 'Create'}</Badge>
                {needsPaymentDetails && <Badge variant="primary">Final approval level</Badge>}
              </div>
              <div className="mt-3 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
                <div><span className="text-slate-500">Requester:</span> <strong className="text-slate-900">{detail.requesterUser?.fullName ?? detail.requesterUser?.userName ?? '—'}</strong></div>
                <div><span className="text-slate-500">Requested on:</span> <strong className="text-slate-900">{new Date(detail.createdAt).toLocaleString()}</strong></div>
                {detail.supportingData && (
                  <div><span className="text-slate-500">Reference:</span> <strong className="text-slate-900">{detail.supportingData}</strong></div>
                )}
                {(detail.sapResponseDocNum || detail.sapResponseDocEntry) && (
                  <div><span className="text-slate-500">SAP Doc:</span> <strong className="text-slate-900">{detail.sapResponseDocNum ?? detail.sapResponseDocEntry}</strong></div>
                )}
              </div>
              {detail.failureReason && (
                <div className="mt-3 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span><strong>SAP posting failed:</strong> {detail.failureReason}</span>
                </div>
              )}
            </div>

            {detail.userApprovals && detail.userApprovals.length > 0 && (
              <div>
                <h3 className="mb-3 text-sm font-semibold text-slate-700">Approval Progress</h3>
                <div className="rounded-xl border border-slate-200 bg-white p-4">
                  <ApprovalTimeline userApprovals={detail.userApprovals} />
                </div>
              </div>
            )}

            <div>
              <h3 className="mb-3 text-sm font-semibold text-slate-700">Request Details</h3>
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
                </div>
              ) : body ? (
                <RequestBodyViewer data={body} />
              ) : (
                <pre className="max-h-48 overflow-auto rounded bg-slate-100 p-3 text-xs">{detail.requestBody}</pre>
              )}
            </div>

            {showActions && (
              <div className="space-y-4 border-t pt-4">
                {needsPaymentDetails && (
                  <div className="flex items-start gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-sm text-primary-800">
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                    <span>This is the final approval level. Approving now will post the payment to SAP — payment date, reference number, and user remarks are all required.</span>
                  </div>
                )}
                {needsPaymentDetails && (
                  <div className="grid gap-4 md:grid-cols-2">
                    <Input label="Payment Date" type="date" value={utrDate} onChange={(e) => setUtrDate(e.target.value)} required />
                    <Input label="Reference No." value={utrNo} onChange={(e) => setUtrNo(e.target.value)} required />
                  </div>
                )}
                <Textarea
                  label={needsPaymentDetails ? 'User Remarks' : 'Comments'}
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  placeholder={needsPaymentDetails ? 'Add remarks (required to finalize this payment)' : 'Add a comment (required to reject)'}
                  required={needsPaymentDetails}
                />
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
