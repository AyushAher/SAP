import { CheckCircle2, Clock, XCircle } from 'lucide-react'
import { Badge } from '@/Components/ui'
import { getApprovalStatusBadgeVariant, getApproverDisplayName, groupApprovalsByLevel } from '@/helpers/approvalUtils'
import type { UserApproval } from '@/Requests/approvals'

function levelStatus(approvers: UserApproval[]): string {
  if (approvers.some((a) => a.approvalStatus === 'Rejected')) return 'Rejected'
  if (approvers.some((a) => a.approvalStatus === 'Approved')) return 'Approved'
  return 'Pending'
}

function LevelIcon({ status }: { status: string }) {
  if (status === 'Approved') return <CheckCircle2 className="h-5 w-5 text-green-600" />
  if (status === 'Rejected') return <XCircle className="h-5 w-5 text-red-600" />
  return <Clock className="h-5 w-5 text-amber-500" />
}

interface ApprovalTimelineProps {
  userApprovals?: UserApproval[]
}

/**
 * Generic multi-level approval progress view — works for every document type, not just Payments,
 * so approvers can always see who already acted and who is still pending at each level.
 */
export function ApprovalTimeline({ userApprovals }: ApprovalTimelineProps) {
  const levels = groupApprovalsByLevel(userApprovals)
  if (levels.length === 0) return null

  return (
    <div>
      {levels.map((level, idx) => {
        const status = levelStatus(level.approvers)
        const isLast = idx === levels.length - 1
        return (
          <div key={level.priority} className="relative flex gap-3 pb-6 last:pb-0">
            {!isLast && <span className="absolute left-[9px] top-6 h-full w-px bg-slate-200" aria-hidden />}
            <div className="relative z-10 mt-0.5 shrink-0">
              <LevelIcon status={status} />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <span className="text-sm font-semibold text-slate-800">Level {level.priority}</span>
                <Badge variant={getApprovalStatusBadgeVariant(status)} size="sm">{status}</Badge>
                {level.approvers.length > 1 && status === 'Pending' && (
                  <span className="text-xs text-slate-400">(any one approver)</span>
                )}
              </div>
              <div className="mt-1.5 space-y-1.5">
                {level.approvers.map((approval, i) => (
                  <div key={i} className="text-sm text-slate-600">
                    <span className="font-medium text-slate-700">{getApproverDisplayName(approval)}</span>
                    {approval.approvalStatus !== 'Pending' && (
                      <span className="text-slate-400"> · {approval.approvalStatus}</span>
                    )}
                    {approval.actionDate && approval.approvalStatus !== 'Pending' && (
                      <span className="text-slate-400"> · {new Date(approval.actionDate).toLocaleString()}</span>
                    )}
                    {approval.comment && (
                      <p className="mt-1 rounded-md bg-slate-50 px-2 py-1 text-xs text-slate-600">“{approval.comment}”</p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          </div>
        )
      })}
    </div>
  )
}
