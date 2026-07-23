import { useEffect, useMemo, useState } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PolicyDialog } from '@/Components/approvals/PolicyDialog'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, Switch } from '@/Components/ui'
import { formatDocumentType } from '@/helpers/approvalUtils'
import {
  deleteApprovalPolicy,
  getApprovalPolicies,
  setApprovalPolicyActive,
  type ApprovalPolicy,
} from '@/Requests/approvalPolicies'
import { getUsersWithRoles, type UserWithRoles } from '@/Requests/userRoles'

function groupApproversByLevel(approvers: ApprovalPolicy['approvers']) {
  const byPriority = new Map<number, number[]>()
  for (const a of approvers) {
    const bucket = byPriority.get(a.priority) ?? []
    bucket.push(a.approverUserId)
    byPriority.set(a.priority, bucket)
  }
  return Array.from(byPriority.entries()).sort(([a], [b]) => a - b)
}

function normalizeRequesterType(value: unknown): 'User' | 'Group' {
  if (value === 1 || value === 'Group' || value === 'group') return 'Group'
  return 'User'
}

export function ApprovalPoliciesPage() {
  const [rows, setRows] = useState<ApprovalPolicy[]>([])
  const [users, setUsers] = useState<UserWithRoles[]>([])
  const [loading, setLoading] = useState(true)
  const [dialogPolicy, setDialogPolicy] = useState<ApprovalPolicy | null | undefined>(undefined)
  const [togglingId, setTogglingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const reload = () => getApprovalPolicies().then(setRows)

  useEffect(() => {
    Promise.all([reload(), getUsersWithRoles().then(setUsers)]).finally(() => setLoading(false))
  }, [])

  const userLabel = useMemo(() => {
    const map = new Map(users.map((u) => [u.id, u.fullName || u.userName || u.email || `User #${u.id}`]))
    return (id: number) => map.get(id) ?? `User #${id}`
  }, [users])

  const handleToggleActive = async (policy: ApprovalPolicy) => {
    setTogglingId(policy.id)
    setError(null)
    try {
      await setApprovalPolicyActive(policy.id, !policy.isActive)
      await reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update policy status')
    } finally {
      setTogglingId(null)
    }
  }

  const handleDelete = async (policy: ApprovalPolicy) => {
    const requesterLabel =
      normalizeRequesterType(policy.requesterType) === 'Group'
        ? (policy.requesterGroupName ?? `Group #${policy.requesterGroupId}`)
        : (policy.requesterName ?? `user #${policy.requesterUserId}`)
    const label = `${formatDocumentType(policy.documentType)} policy for ${requesterLabel}`
    if (!window.confirm(`Delete the ${label}? This cannot be undone.`)) return
    await deleteApprovalPolicy(policy.id)
    await reload()
  }

  const requesterDisplay = (policy: ApprovalPolicy) => {
    if (normalizeRequesterType(policy.requesterType) === 'Group') {
      return (
        <span className="inline-flex items-center gap-1.5">
          <Badge variant="default">Group</Badge>
          {policy.requesterGroupName ?? `Group #${policy.requesterGroupId}`}
        </span>
      )
    }
    return (
      <span className="inline-flex items-center gap-1.5">
        <Badge variant="default">User</Badge>
        {policy.requesterName ?? `User #${policy.requesterUserId}`}
      </span>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Approval Policies"
        description="Configure multi-level approval rules"
        actionLabel="Add Policy"
        onAction={() => setDialogPolicy(null)}
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      <SapDataGrid
        loading={loading}
        data={rows}
        getRowKey={(r) => r.id}
        emptyMessage="No approval policies configured yet. Click “Add Policy” to define the first approval rule."
        columns={[
          { key: 'id', header: 'ID', accessor: (r) => r.id },
          { key: 'documentType', header: 'Document Type', accessor: (r) => formatDocumentType(r.documentType) },
          { key: 'requester', header: 'Requester', render: (r) => requesterDisplay(r) },
          {
            key: 'approvers',
            header: 'Approvers',
            render: (r) => (
              <div className="flex flex-wrap gap-1">
                {groupApproversByLevel(r.approvers).map(([priority, userIds]) => (
                  <span key={priority} className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                    <strong>L{priority}</strong> {userIds.map((id) => userLabel(id)).join(' / ')}
                  </span>
                ))}
              </div>
            ),
          },
          { key: 'rules', header: 'Rules', accessor: (r) => r.rules.length || '—' },
          {
            key: 'active',
            header: 'Status',
            render: (r) => (
              <div className="flex items-center gap-2">
                <Badge variant={r.isActive ? 'success' : 'default'}>{r.isActive ? 'Active' : 'Inactive'}</Badge>
                <Switch
                  checked={r.isActive}
                  disabled={togglingId === r.id}
                  onChange={() => void handleToggleActive(r)}
                  aria-label={r.isActive ? 'Deactivate policy' : 'Activate policy'}
                />
              </div>
            ),
          },
        ]}
        actions={(row) => (
          <RowActions>
            <RowActionButton
              title="Edit policy"
              icon={<Pencil className={rowActionIconClassName} />}
              onClick={() => setDialogPolicy(row)}
            />
            <RowActionButton
              title="Delete policy"
              variant="danger"
              icon={<Trash2 className={rowActionIconClassName} />}
              onClick={() => void handleDelete(row)}
            />
          </RowActions>
        )}
      />

      <PolicyDialog
        policy={dialogPolicy ?? null}
        isOpen={dialogPolicy !== undefined}
        onClose={() => setDialogPolicy(undefined)}
        onSaved={reload}
      />
    </div>
  )
}
