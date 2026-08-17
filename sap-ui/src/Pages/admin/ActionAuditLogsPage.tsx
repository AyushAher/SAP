import { useMemo } from 'react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { DataTable, type DataTableColumn, Badge, Card, CardContent } from '@/Components/ui'
import { formatDate } from '@/helpers/lib/utils'
import { listActionAuditLogs, type ActionAuditLogRow } from '@/Requests/auditLogs'

function statusVariant(code: number): 'success' | 'danger' | 'warning' | 'default' {
  if (code >= 200 && code < 300) return 'success'
  if (code >= 400 && code < 500) return 'warning'
  if (code >= 500) return 'danger'
  return 'default'
}

export function ActionAuditLogsPage() {
  const columns = useMemo<DataTableColumn<ActionAuditLogRow>[]>(() => [
    {
      key: 'createdAt',
      header: 'When',
      sortable: true,
      accessor: (row) => (row.createdAt ? formatDate(row.createdAt) : '—'),
    },
    {
      key: 'action',
      header: 'Action',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (row) => row.action,
    },
    {
      key: 'userName',
      header: 'User',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (row) => row.userName ?? (row.userId != null ? `#${row.userId}` : '—'),
    },
    {
      key: 'companyDb',
      header: 'Company',
      sortable: true,
      filterable: true,
      filterOperator: 'eq',
      accessor: (row) => row.companyDb ?? '—',
    },
    {
      key: 'httpMethod',
      header: 'Method',
      accessor: (row) => row.httpMethod,
    },
    {
      key: 'path',
      header: 'Path',
      filterable: true,
      filterOperator: 'contains',
      accessor: (row) => row.path,
    },
    {
      key: 'statusCode',
      header: 'Status',
      sortable: true,
      filterable: true,
      filterOperator: 'eq',
      render: (row) => (
        <Badge variant={statusVariant(row.statusCode)}>{row.statusCode}</Badge>
      ),
    },
    {
      key: 'durationMs',
      header: 'Duration',
      accessor: (row) => `${row.durationMs} ms`,
    },
    {
      key: 'ipAddress',
      header: 'IP',
      accessor: (row) => row.ipAddress ?? '—',
    },
  ], [])

  return (
    <div className="space-y-6">
      <PageHeader
        title="Action Audit Logs"
        description="Append-only record of create, update, delete, and authentication actions."
      />
      <Card>
        <CardContent className="!p-0 pt-0">
          <DataTable
            columns={columns}
            fetchData={listActionAuditLogs}
            getRowKey={(row) => row.id}
            initialSorts={[{ field: 'createdAt', direction: 'desc' }]}
            defaultPageSize={20}
            emptyMessage="No action logs match your criteria"
          />
        </CardContent>
      </Card>
    </div>
  )
}
