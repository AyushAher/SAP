import { useEffect, useState } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { UserGroupDialog } from '@/Components/users/UserGroupDialog'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Badge, Switch } from '@/Components/ui'
import {
  deleteUserGroup,
  getUserGroups,
  setUserGroupActive,
  type UserGroup,
} from '@/Requests/userGroups'

function memberLabel(m: UserGroup['members'][number]) {
  return m.fullName || m.userName || m.email || `User #${m.userId}`
}

export function UserGroupsPage() {
  const [rows, setRows] = useState<UserGroup[]>([])
  const [loading, setLoading] = useState(true)
  const [dialogGroup, setDialogGroup] = useState<UserGroup | null | undefined>(undefined)
  const [togglingId, setTogglingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const reload = () => getUserGroups().then(setRows)

  useEffect(() => {
    void reload().finally(() => setLoading(false))
  }, [])

  const handleToggleActive = async (group: UserGroup) => {
    setTogglingId(group.id)
    setError(null)
    try {
      await setUserGroupActive(group.id, !group.isActive)
      await reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update group status')
    } finally {
      setTogglingId(null)
    }
  }

  const handleDelete = async (group: UserGroup) => {
    if (!window.confirm(`Delete group “${group.name}”? This cannot be undone.`)) return
    setError(null)
    try {
      await deleteUserGroup(group.id)
      await reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete group')
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="User Groups"
        description="Manage requester groups for approval policies"
        actionLabel="Add Group"
        onAction={() => setDialogGroup(null)}
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      <SapDataGrid
        loading={loading}
        data={rows}
        getRowKey={(r) => r.id}
        emptyMessage="No user groups yet. Click “Add Group” to create one."
        columns={[
          { key: 'id', header: 'ID', accessor: (r) => r.id },
          { key: 'name', header: 'Name', accessor: (r) => r.name },
          {
            key: 'members',
            header: 'Members',
            render: (r) =>
              r.members.length === 0 ? (
                <span className="text-slate-400">—</span>
              ) : (
                <div className="flex flex-wrap gap-1">
                  {r.members.map((m) => (
                    <span key={m.userId} className="rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                      {memberLabel(m)}
                    </span>
                  ))}
                </div>
              ),
          },
          {
            key: 'description',
            header: 'Description',
            accessor: (r) => r.description || '—',
          },
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
                  aria-label={r.isActive ? 'Deactivate group' : 'Activate group'}
                />
              </div>
            ),
          },
        ]}
        actions={(row) => (
          <RowActions>
            <RowActionButton
              title="Edit group"
              icon={<Pencil className={rowActionIconClassName} />}
              onClick={() => setDialogGroup(row)}
            />
            <RowActionButton
              title="Delete group"
              variant="danger"
              icon={<Trash2 className={rowActionIconClassName} />}
              onClick={() => void handleDelete(row)}
            />
          </RowActions>
        )}
      />

      <UserGroupDialog
        group={dialogGroup ?? null}
        isOpen={dialogGroup !== undefined}
        onClose={() => setDialogGroup(undefined)}
        onSaved={() => void reload()}
      />
    </div>
  )
}
