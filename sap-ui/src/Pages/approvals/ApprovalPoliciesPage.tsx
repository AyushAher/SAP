import { useEffect, useState } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PolicyDialog } from '@/Components/approvals/PolicyDialog'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { deleteApprovalPolicy, getApprovalPolicies, type ApprovalPolicy } from '@/Requests/approvalPolicies'

export function ApprovalPoliciesPage() {
  const [rows, setRows] = useState<ApprovalPolicy[]>([])
  const [loading, setLoading] = useState(true)
  const [dialogPolicy, setDialogPolicy] = useState<ApprovalPolicy | null | undefined>(undefined)

  const reload = () => getApprovalPolicies().then(setRows)

  useEffect(() => {
    reload().finally(() => setLoading(false))
  }, [])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Approval Policies"
        description="Configure multi-level approval rules"
        actionLabel="Add Policy"
        onAction={() => setDialogPolicy(null)}
      />
      <SapDataGrid
        loading={loading}
        data={rows}
        getRowKey={(r) => r.id}
        columns={[
          { key: 'id', header: 'ID', accessor: (r) => r.id },
          { key: 'documentType', header: 'Document Type', accessor: (r) => r.documentType },
          { key: 'requester', header: 'Requester', accessor: (r) => r.requesterName },
          { key: 'approvers', header: 'Approvers', accessor: (r) => r.approvers.length },
          {
            key: 'levels',
            header: 'Levels',
            accessor: (r) => new Set(r.approvers.map((a) => a.priority)).size,
          },
          { key: 'rules', header: 'Rules', accessor: (r) => r.rules.length },
          { key: 'active', header: 'Active', accessor: (r) => r.isActive ? 'Yes' : 'No' },
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
              onClick={() => deleteApprovalPolicy(row.id).then(reload)}
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
