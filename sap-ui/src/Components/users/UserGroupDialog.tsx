import { useEffect, useState } from 'react'
import { Button, Input, Modal, MultiSelect, Textarea } from '@/Components/ui'
import { createUserGroup, updateUserGroup, type UserGroup } from '@/Requests/userGroups'
import { getUsersWithRoles, type UserWithRoles } from '@/Requests/userRoles'

interface UserGroupDialogProps {
  group: UserGroup | null
  isOpen: boolean
  onClose: () => void
  onSaved: () => void
}

function userLabel(u: UserWithRoles) {
  const name = u.fullName || u.userName || u.email
  return name ? `${name}${u.email && name !== u.email ? ` (${u.email})` : ''}` : `User #${u.id}`
}

export function UserGroupDialog({ group, isOpen, onClose, onSaved }: UserGroupDialogProps) {
  const [users, setUsers] = useState<UserWithRoles[]>([])
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [memberUserIds, setMemberUserIds] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isOpen) return
    setLoading(true)
    setError(null)
    void getUsersWithRoles()
      .then(setUsers)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load users'))
      .finally(() => setLoading(false))
  }, [isOpen])

  useEffect(() => {
    if (!isOpen) return
    if (group) {
      setName(group.name)
      setDescription(group.description ?? '')
      setMemberUserIds(group.members.map((m) => String(m.userId)))
    } else {
      setName('')
      setDescription('')
      setMemberUserIds([])
    }
  }, [isOpen, group])

  const userOptions = users.map((u) => ({ value: String(u.id), label: userLabel(u) }))

  const handleSave = async () => {
    if (!name.trim()) {
      setError('Group name is required.')
      return
    }

    setSaving(true)
    setError(null)
    try {
      const payload = {
        name: name.trim(),
        description: description.trim() || undefined,
        memberUserIds: memberUserIds.map(Number).filter((id) => id > 0),
      }
      if (group) await updateUserGroup(group.id, payload)
      else await createUserGroup(payload)
      onSaved()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={group ? 'Edit User Group' : 'Add User Group'} size="lg">
      <div className="space-y-5">
        {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

        {loading ? (
          <div className="py-8 text-center text-slate-500">Loading users...</div>
        ) : (
          <>
            <Input label="Group Name" required value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Purchase Team" />
            <Textarea
              label="Description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description"
              rows={3}
            />
            <MultiSelect
              label="Members"
              options={userOptions}
              value={memberUserIds}
              onChange={setMemberUserIds}
              placeholder={userOptions.length ? 'Select users' : 'No users available'}
              hint="Each user can belong to only one group."
            />
          </>
        )}

        <div className="flex justify-end gap-3 border-t pt-4">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={handleSave} isLoading={saving} disabled={loading}>Save</Button>
        </div>
      </div>
    </Modal>
  )
}
