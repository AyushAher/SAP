import { useEffect, useState } from 'react'
import { Save } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Card, CardContent } from '@/Components/ui'
import { getRoles, getUsersWithRoles, updateUserRoles, type UserWithRoles } from '@/Requests/userRoles'
import { ROLES } from '@/config/constants'

export function UserRoleManagementPage() {
  const [users, setUsers] = useState<UserWithRoles[]>([])
  const [roles, setRoles] = useState<string[]>([])
  const [selections, setSelections] = useState<Record<number, string[]>>({})
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    Promise.all([getUsersWithRoles(), getRoles()])
      .then(([u, r]) => {
        setUsers(u)
        setRoles(r.length ? r : [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.STANDARD])
        setSelections(Object.fromEntries(u.map((user) => [user.id, user.roles])))
      })
      .finally(() => setLoading(false))
  }, [])

  const toggleRole = (userId: number, role: string) => {
    setSelections((prev) => {
      const current = prev[userId] ?? []
      return {
        ...prev,
        [userId]: current.includes(role) ? current.filter((r) => r !== role) : [...current, role],
      }
    })
  }

  const save = async (userId: number) => {
    await updateUserRoles(userId, selections[userId] ?? [])
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  return (
    <div className="space-y-6">
      <PageHeader title="User Role Management" description={`Total users: ${users.length}`} />
      <div className="space-y-4">
        {users.map((user) => (
          <Card key={user.id}>
            <CardContent className="flex flex-col gap-4 pt-6 md:flex-row md:items-center md:justify-between">
              <div>
                <p className="font-medium">{user.fullName ?? user.userName}</p>
                <p className="text-sm text-slate-500">{user.email}</p>
              </div>
              <div className="flex flex-wrap gap-3">
                {roles.map((role) => (
                  <label key={role} className="flex items-center gap-2 text-sm">
                    <input type="checkbox" checked={(selections[user.id] ?? []).includes(role)} onChange={() => toggleRole(user.id, role)} />
                    {role}
                  </label>
                ))}
              </div>
              <RowActionButton
                title="Save roles"
                variant="primary"
                icon={<Save className={rowActionIconClassName} />}
                onClick={() => save(user.id)}
              />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
