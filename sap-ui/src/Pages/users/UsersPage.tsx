import { useEffect, useState } from 'react'
import { Users } from 'lucide-react'
import { DataTable, type DataTableColumn } from '@/Components/ui/DataTable'
import { Badge, Card, CardContent, CardHeader, CardTitle } from '@/Components/ui'
import { GetUsers } from '@/Requests/GetUsers'
import type { User } from '@/types'

const columns: DataTableColumn<User>[] = [
  {
    key: 'name',
    header: 'Name',
    sortable: true,
    filterable: true,
    filterPlaceholder: 'Search name...',
  },
  {
    key: 'email',
    header: 'Email',
    sortable: true,
    filterable: true,
    filterPlaceholder: 'Search email...',
  },
  {
    key: 'role',
    header: 'Role',
    sortable: true,
    filterable: true,
    filterType: 'select',
    filterOperator: 'eq',
    filterOptions: [
      { value: 'admin', label: 'Admin' },
      { value: 'editor', label: 'Editor' },
      { value: 'viewer', label: 'Viewer' },
    ],
    render: (row) => (
      <Badge
        variant={
          row.role === 'admin' ? 'primary' : row.role === 'editor' ? 'success' : 'default'
        }
      >
        {row.role}
      </Badge>
    ),
  },
]

export function UsersPage() {
  const [directFetchCount, setDirectFetchCount] = useState<number | null>(null)

  useEffect(() => {
    async function loadDirect() {
      const users = await GetUsers()
      const usersList = users.data ?? []
      setDirectFetchCount(usersList.length)
    }
    loadDirect()
  }, [])

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Users</h1>
        <p className="mt-1 text-sm text-slate-500">
          Server-side paginated data table with sorting and filtering.
          {directFetchCount !== null && (
            <span className="ml-1">
              Direct fetch returned <strong>{directFetchCount}</strong> users (default page).
            </span>
          )}
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5 text-primary-600" />
            User Management
          </CardTitle>
        </CardHeader>
        <CardContent className="!p-0">
          <DataTable
            columns={columns}
            fetchData={GetUsers}
            getRowKey={(row) => row.id}
            defaultPageSize={10}
            emptyMessage="No users match your criteria"
          />
        </CardContent>
      </Card>
    </div>
  )
}
