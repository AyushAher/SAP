import { getApprovalRequest } from '@/Requests/approvals'
import { toast } from '@/helpers/toast'

const POLL_INTERVAL_MS = 3000
const MAX_ATTEMPTS = 20 // ~1 minute total

/**
 * Approve queues SAP posting to a background job and returns immediately, so there is no doc
 * number to show at click time. Polls the approval record until the background job finishes
 * (doc number posted, or a failure reason recorded) and raises a floating notification with the
 * outcome — the request is fire-and-forget and outlives whatever dialog/page triggered it, since
 * `toast` is a module-level singleton, not tied to a component's lifecycle.
 */
export function pollApprovalOutcome(requestId: number, documentLabel = 'Request'): void {
  let attempts = 0

  const tick = async () => {
    attempts += 1
    try {
      const request = await getApprovalRequest(requestId)
      const docNo = request?.sapResponseDocNum ?? request?.sapResponseDocEntry
      if (docNo) {
        toast.success(`${documentLabel} posted to SAP. Doc No: ${docNo}`)
        return
      }
      if (request?.failureReason) {
        toast.error(`${documentLabel} SAP posting failed: ${request.failureReason}`)
        return
      }
    } catch {
      // Transient fetch error — keep trying until the attempt budget runs out.
    }
    if (attempts < MAX_ATTEMPTS)
      window.setTimeout(() => void tick(), POLL_INTERVAL_MS)
  }

  window.setTimeout(() => void tick(), POLL_INTERVAL_MS)
}
