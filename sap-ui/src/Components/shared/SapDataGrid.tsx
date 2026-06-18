import { useMemo } from 'react'
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/Components/ui'
import { Spinner } from '@/Components/ui'

export interface SapColumn<T> {
  key: string
  header: string
  render?: (row: T) => React.ReactNode
  accessor?: (row: T) => string | number | null | undefined
}

interface SapDataGridProps<T> {
  columns: SapColumn<T>[]
  data: T[]
  loading?: boolean
  emptyMessage?: string
  getRowKey: (row: T) => string | number
  actions?: (row: T) => React.ReactNode
  onRowClick?: (row: T) => void
  isRowSelected?: (row: T) => boolean
  maxHeight?: string
}

export function SapDataGrid<T>({
  columns,
  data,
  loading,
  emptyMessage = 'No records',
  getRowKey,
  actions,
  onRowClick,
  isRowSelected,
  maxHeight,
}: SapDataGridProps<T>) {
  const rows = useMemo(() => data, [data])

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner />
      </div>
    )
  }

  return (
    <div
      className="overflow-auto rounded-xl border border-slate-200 bg-white"
      style={maxHeight ? { maxHeight } : undefined}
    >
      <Table className="min-w-full">
        <TableHeader>
          <TableRow>
            {columns.map((col) => (
              <TableHead key={col.key}>{col.header}</TableHead>
            ))}
            {actions && <TableHead>Actions</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={columns.length + (actions ? 1 : 0)} className="text-center text-slate-500">
                {emptyMessage}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => (
              <TableRow
                key={getRowKey(row)}
                className={onRowClick ? 'cursor-pointer' : undefined}
                data-selected={isRowSelected?.(row) ? 'true' : undefined}
                onClick={() => onRowClick?.(row)}
              >
                {columns.map((col) => (
                  <TableCell key={col.key} className="max-w-[12rem] whitespace-normal break-words align-top">
                    {col.render ? col.render(row) : String(col.accessor?.(row) ?? (row as Record<string, unknown>)[col.key] ?? '')}
                  </TableCell>
                ))}
                {actions && <TableCell>{actions(row)}</TableCell>}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  )
}
