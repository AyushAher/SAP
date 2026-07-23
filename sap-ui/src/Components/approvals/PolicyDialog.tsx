import { useEffect, useState } from 'react'
import { Info, Trash2 } from 'lucide-react'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Button, Input, Modal, Select } from '@/Components/ui'
import { formatDocumentType } from '@/helpers/approvalUtils'
import {
  createApprovalPolicy,
  getApprovalPolicyMetadata,
  updateApprovalPolicy,
  type ApprovalPolicy,
  type ApprovalRequesterType,
} from '@/Requests/approvalPolicies'
import { getUserGroups, type UserGroup } from '@/Requests/userGroups'
import { getUsersWithRoles, type UserWithRoles } from '@/Requests/userRoles'

interface PolicyDialogProps {
  policy: ApprovalPolicy | null
  isOpen: boolean
  onClose: () => void
  onSaved: () => void
}

interface ApproverRow {
  approverUserId: number
  priority: number
}

interface RuleRow {
  fieldName: string
  operator: string
  value: string
}

// "DocTotal" means something different (and, for Payments, is resolved to a different underlying
// field entirely) depending on document type — clarify in the rule builder so admins don't assume it
// always means "the PO's total value".
function fieldLabel(documentType: string, fieldName: string): string {
  if (fieldName !== 'DocTotal') return fieldName
  switch (documentType) {
    case 'Payments':
      return 'DocTotal (this payment\u2019s transfer amount)'
    case 'StagewisePayments_DP':
      return 'DocTotal (this down payment\u2019s amount)'
    case 'PurchaseOrder':
      return 'DocTotal (PO total value)'
    default:
      return fieldName
  }
}

function normalizeRequesterType(value: unknown): ApprovalRequesterType {
  if (value === 1 || value === 'Group' || value === 'group') return 'Group'
  return 'User'
}

export function PolicyDialog({ policy, isOpen, onClose, onSaved }: PolicyDialogProps) {
  const [users, setUsers] = useState<UserWithRoles[]>([])
  const [groups, setGroups] = useState<UserGroup[]>([])
  const [documentTypes, setDocumentTypes] = useState<string[]>([])
  const [fieldsByType, setFieldsByType] = useState<Record<string, string[]>>({})
  const [operators, setOperators] = useState<string[]>([])
  const [documentType, setDocumentType] = useState('')
  const [requesterType, setRequesterType] = useState<ApprovalRequesterType>('User')
  const [requesterUserId, setRequesterUserId] = useState('')
  const [requesterGroupId, setRequesterGroupId] = useState('')
  const [approvers, setApprovers] = useState<ApproverRow[]>([{ approverUserId: 0, priority: 1 }])
  const [rules, setRules] = useState<RuleRow[]>([])
  const [saving, setSaving] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isOpen) return

    setLoading(true)
    setError(null)

    void Promise.all([getUsersWithRoles(), getUserGroups(), getApprovalPolicyMetadata()])
      .then(([userList, groupList, metadata]) => {
        setUsers(userList)
        setGroups(groupList.filter((g) => g.isActive || (policy?.requesterGroupId != null && g.id === policy.requesterGroupId)))
        setDocumentTypes(metadata.documentTypes.filter((t) => t !== 'None'))
        setFieldsByType(metadata.fields)
        setOperators(metadata.operators)
        if (userList.length === 0) {
          setError('No users found. Register users before creating approval policies.')
        }
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load policy form data')
      })
      .finally(() => setLoading(false))
  }, [isOpen, policy?.requesterGroupId])

  useEffect(() => {
    if (!isOpen) return
    if (policy) {
      setDocumentType(String(policy.documentType ?? ''))
      setRequesterType(normalizeRequesterType(policy.requesterType))
      setRequesterUserId(policy.requesterUserId != null ? String(policy.requesterUserId) : '')
      setRequesterGroupId(policy.requesterGroupId != null ? String(policy.requesterGroupId) : '')
      setApprovers(policy.approvers.length ? policy.approvers : [{ approverUserId: 0, priority: 1 }])
      setRules(policy.rules.map((r) => ({ fieldName: r.fieldName, operator: r.operator, value: r.value })))
    } else {
      setDocumentType('')
      setRequesterType('User')
      setRequesterUserId('')
      setRequesterGroupId('')
      setApprovers([{ approverUserId: 0, priority: 1 }])
      setRules([])
    }
  }, [isOpen, policy])

  const userLabel = (u: UserWithRoles) => {
    const name = u.fullName || u.userName || u.email
    return name ? `${name}${u.email && name !== u.email ? ` (${u.email})` : ''}` : `User #${u.id}`
  }

  const userOptions = users.map((u) => ({ value: String(u.id), label: userLabel(u) }))
  const groupOptions = groups.map((g) => ({
    value: String(g.id),
    label: `${g.name}${g.members.length ? ` (${g.members.length} members)` : ''}`,
  }))
  const fieldOptions = (fieldsByType[documentType] ?? []).map((f) => ({ value: f, label: fieldLabel(documentType, f) }))
  const operatorOptions = operators.map((o) => ({ value: o, label: o }))
  const docTypeOptions = documentTypes.map((t) => ({ value: t, label: formatDocumentType(t) }))
  const requesterTypeOptions = [
    { value: 'User', label: 'User' },
    { value: 'Group', label: 'Group' },
  ]

  const nextApproverPriority = () => (approvers.length ? Math.max(...approvers.map((a) => a.priority)) + 1 : 1)

  const handleRequesterTypeChange = (value: string) => {
    const next = value === 'Group' ? 'Group' : 'User'
    setRequesterType(next)
    if (next === 'User') setRequesterGroupId('')
    else setRequesterUserId('')
  }

  const handleSave = async () => {
    if (!documentType) {
      setError('Document type is required.')
      return
    }
    if (requesterType === 'User' && !requesterUserId) {
      setError('Requester user is required.')
      return
    }
    if (requesterType === 'Group' && !requesterGroupId) {
      setError('Requester group is required.')
      return
    }
    const validApprovers = approvers.filter((a) => a.approverUserId > 0)
    if (validApprovers.length === 0) {
      setError('At least one approver is required.')
      return
    }
    if (new Set(validApprovers.map((a) => a.approverUserId)).size !== validApprovers.length) {
      setError('Duplicate approvers are not allowed.')
      return
    }

    setSaving(true)
    setError(null)
    try {
      const payload = {
        documentType,
        requesterType,
        requesterUserId: requesterType === 'User' ? Number(requesterUserId) : null,
        requesterGroupId: requesterType === 'Group' ? Number(requesterGroupId) : null,
        approvers: validApprovers,
        rules: rules.filter((r) => r.fieldName && r.operator && r.value),
      }
      if (policy) await updateApprovalPolicy(policy.id, payload)
      else await createApprovalPolicy(payload)
      onSaved()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={policy ? 'Edit Approval Policy' : 'Add Approval Policy'} size="2xl">
      <div className="space-y-6">
        {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

        {loading ? (
          <div className="py-8 text-center text-slate-500">Loading users and document types...</div>
        ) : (
          <>
            <div className="grid gap-4 md:grid-cols-3">
              <Select
                label="Requester Type"
                required
                value={requesterType}
                onChange={handleRequesterTypeChange}
                options={requesterTypeOptions}
              />
              {requesterType === 'User' ? (
                <Select
                  label="Requesting User"
                  required
                  value={requesterUserId}
                  onChange={setRequesterUserId}
                  placeholder={userOptions.length ? 'Select user' : 'No users available'}
                  options={[{ value: '', label: 'Select user' }, ...userOptions]}
                />
              ) : (
                <Select
                  label="Requesting Group"
                  required
                  value={requesterGroupId}
                  onChange={setRequesterGroupId}
                  placeholder={groupOptions.length ? 'Select group' : 'No groups available'}
                  options={[{ value: '', label: 'Select group' }, ...groupOptions]}
                  hint={groupOptions.length === 0 ? 'Create a user group first from User Groups.' : undefined}
                />
              )}
              <Select
                label="Document Type"
                required
                value={documentType}
                onChange={setDocumentType}
                placeholder={docTypeOptions.length ? 'Select document type' : 'No document types available'}
                options={[{ value: '', label: 'Select type' }, ...docTypeOptions]}
              />
            </div>
            <p className="flex items-start gap-1.5 text-xs text-slate-500">
              <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              Groups apply only to the requester. Approvers remain individual users. When both a user policy and a group
              policy exist, the user policy takes priority.
            </p>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-1 flex items-center justify-between">
                <h3 className="font-semibold text-slate-800">Approvers</h3>
                <Button size="sm" variant="outline" onClick={() => setApprovers([...approvers, { approverUserId: 0, priority: nextApproverPriority() }])}>Add Approver</Button>
              </div>
              <p className="mb-3 flex items-start gap-1.5 text-xs text-slate-500">
                <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                Priority determines the approval order. Approvers sharing the same priority form a level where any one of them can approve to move to the next level.
              </p>
              <div className="space-y-3">
                {approvers.map((row, idx) => (
                  <div key={idx} className="grid gap-3 rounded-lg border bg-white p-3 md:grid-cols-[2fr_1fr_auto]">
                    <Select
                      label="User"
                      value={String(row.approverUserId || '')}
                      onChange={(value) => {
                        const next = [...approvers]
                        next[idx] = { ...row, approverUserId: Number(value) }
                        setApprovers(next)
                      }}
                      options={[{ value: '', label: 'Select user' }, ...userOptions]}
                    />
                    <Input
                      label="Priority"
                      type="number"
                      min={1}
                      value={String(row.priority)}
                      onChange={(e) => {
                        const next = [...approvers]
                        next[idx] = { ...row, priority: Number(e.target.value) }
                        setApprovers(next)
                      }}
                    />
                    <div className="flex items-end">
                      <RowActionButton
                        title="Remove approver"
                        variant="danger"
                        icon={<Trash2 className={rowActionIconClassName} />}
                        onClick={() => setApprovers(approvers.filter((_, i) => i !== idx))}
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-1 flex items-center justify-between">
                <h3 className="font-semibold text-slate-800">Approval Rules</h3>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!documentType}
                  onClick={() => setRules([...rules, { fieldName: '', operator: operators[0] ?? 'Equal', value: '' }])}
                >
                  Add Rule
                </Button>
              </div>
              <p className="mb-3 text-xs text-slate-500">
                {documentType
                  ? 'Optional: only route requests through this policy when all rules match. Leave empty to apply the policy to every request from this requester.'
                  : 'Select a document type to configure rules.'}
              </p>
              <div className="space-y-3">
                {rules.length === 0 && documentType && (
                  <p className="rounded-lg border border-dashed border-slate-300 bg-white px-3 py-4 text-center text-sm text-slate-400">
                    No rules — this policy applies to all requests from this requester.
                  </p>
                )}
                {rules.map((row, idx) => (
                  <div key={idx} className="grid gap-3 rounded-lg border bg-white p-3 md:grid-cols-[1fr_1fr_1fr_auto]">
                    <Select
                      label="Field"
                      value={row.fieldName}
                      onChange={(value) => {
                        const next = [...rules]
                        next[idx] = { ...row, fieldName: value }
                        setRules(next)
                      }}
                      options={[{ value: '', label: 'Select field' }, ...fieldOptions]}
                    />
                    <Select
                      label="Operator"
                      value={row.operator}
                      onChange={(value) => {
                        const next = [...rules]
                        next[idx] = { ...row, operator: value }
                        setRules(next)
                      }}
                      options={operatorOptions.length ? operatorOptions : [{ value: 'Equal', label: 'Equal' }]}
                    />
                    <Input
                      label="Value"
                      value={row.value}
                      onChange={(e) => {
                        const next = [...rules]
                        next[idx] = { ...row, value: e.target.value }
                        setRules(next)
                      }}
                    />
                    <div className="flex items-end">
                      <RowActionButton
                        title="Remove rule"
                        variant="danger"
                        icon={<Trash2 className={rowActionIconClassName} />}
                        onClick={() => setRules(rules.filter((_, i) => i !== idx))}
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>
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
