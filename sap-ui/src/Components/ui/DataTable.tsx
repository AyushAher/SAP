import { ArrowDown, ArrowUp, ArrowUpDown, RefreshCw, X } from 'lucide-react'
import { rowActionsCellClassName, rowActionsHeaderClassName } from '@/Components/shared/RowActions'
import { cn } from '@/helpers/lib/utils'
import { DEFAULT_PAGE_SIZE, DEFAULT_PAGE_SIZE_OPTIONS } from '@/helpers/api/pagination'
import type { Filter, FilterOperator, PaginationRequest, PaginationResponse, Sort } from '@/types/api'
import type { SelectOption } from '@/types'
import { useDataTable } from './useDataTable'
import { DataTablePagination } from './DataTablePagination'
import { Button } from './Button'
import { Input } from './Input'
import { Select } from './Select'
import { Spinner } from './Spinner'
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from './Table'

export interface DataTableColumn<T> {
  key: string
  header: string
  sortable?: boolean
  filterable?: boolean
  filterOperator?: FilterOperator
  filterPlaceholder?: string
  filterType?: 'text' | 'select'
  filterOptions?: SelectOption[]
  className?: string
  headerClassName?: string
  cellClassName?: string
  render?: (row: T, index: number) => React.ReactNode
  accessor?: (row: T) => string | number | boolean | null | undefined
}

export interface DataTableProps<T> {
  columns: DataTableColumn<T>[]
  fetchData: (request: PaginationRequest) => Promise<PaginationResponse<T[]>>
  getRowKey: (row: T) => string | number
  defaultPageSize?: number
  pageSizeOptions?: number[]
  initialFilters?: Filter[]
  initialSorts?: Sort[]
  emptyMessage?: string
  className?: string
  toolbar?: React.ReactNode
  onRowClick?: (row: T) => void
}

function isActionsColumn(column: { key: string }) {
  return column.key === 'actions'
}

function getCellValue<T>(row: T, column: DataTableColumn<T>): unknown {
  if (column.accessor) return column.accessor(row)
  return (row as Record<string, unknown>)[column.key]
}

function SortIcon({ direction }: { direction: 'asc' | 'desc' | null }) {
  if (direction === 'asc') return <ArrowUp className="h-3.5 w-3.5 text-primary-600" />
  if (direction === 'desc') return <ArrowDown className="h-3.5 w-3.5 text-primary-600" />
  return <ArrowUpDown className="h-3.5 w-3.5 text-slate-400" />
}

export function DataTable<T>({
  columns,
  fetchData,
  getRowKey,
  defaultPageSize = DEFAULT_PAGE_SIZE,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  initialFilters,
  initialSorts,
  emptyMessage = 'No records found',
  className,
  toolbar,
  onRowClick,
}: DataTableProps<T>) {
  const {
    data,
    loading,
    error,
    request,
    totalCount,
    pageCount,
    filterValues,
    setPage,
    setPageSize,
    setFilter,
    clearFilters,
    toggleColumnSort,
    refresh,
    getSortDirection,
  } = useDataTable({
    fetchData,
    defaultPageSize,
    initialFilters,
    initialSorts,
  })

  const hasFilters = columns.some((col) => col.filterable)
  const hasActiveFilters = request.filters.length > 0

  return (
    <div className={cn('overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm', className)}>
      {(toolbar || hasActiveFilters) && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <div className="flex-1">{toolbar}</div>
          <div className="flex items-center gap-2">
            {hasActiveFilters && (
              <Button variant="ghost" size="sm" onClick={clearFilters} leftIcon={<X className="h-4 w-4" />}>
                Clear filters
              </Button>
            )}
            <Button
              variant="outline"
              size="sm"
              onClick={refresh}
              disabled={loading}
              leftIcon={<RefreshCw className={cn('h-4 w-4', loading && 'animate-spin')} />}
            >
              Refresh
            </Button>
          </div>
        </div>
      )}

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {error}
        </div>
      )}

      <div className="relative">
        {loading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-white/70">
            <Spinner size="lg" />
          </div>
        )}

        <Table className="!rounded-none !border-0">
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              {columns.map((column) => (
                <TableHead
                  key={column.key}
                  className={cn(
                    column.sortable && 'cursor-pointer select-none',
                    isActionsColumn(column) && rowActionsHeaderClassName,
                    column.headerClassName,
                  )}
                  onClick={() => column.sortable && toggleColumnSort(column.key)}
                >
                  <div className="flex items-center gap-1.5">
                    <span>{column.header}</span>
                    {column.sortable && <SortIcon direction={getSortDirection(column.key)} />}
                  </div>
                </TableHead>
              ))}
            </TableRow>

            {hasFilters && (
              <TableRow className="bg-white hover:bg-white">
                {columns.map((column) => (
                  <TableHead key={`filter-${column.key}`} className="!py-2 !font-normal !normal-case">
                    {column.filterable ? (
                      column.filterType === 'select' && column.filterOptions ? (
                        <Select
                          options={[{ value: '', label: 'All' }, ...column.filterOptions]}
                          value={filterValues[column.key] ?? ''}
                          onChange={(value) =>
                            setFilter(column.key, value, column.filterOperator ?? 'eq')
                          }
                          placeholder={column.filterPlaceholder ?? `Filter ${column.header}`}
                        />
                      ) : (
                        <Input
                          placeholder={column.filterPlaceholder ?? `Filter ${column.header}`}
                          value={filterValues[column.key] ?? ''}
                          onChange={(e) =>
                            setFilter(column.key, e.target.value, column.filterOperator ?? 'contains')
                          }
                          className="!py-1.5 text-xs"
                        />
                      )
                    ) : null}
                  </TableHead>
                ))}
              </TableRow>
            )}
          </TableHeader>

          <TableBody>
            {data.length === 0 && !loading ? (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={columns.length} className="py-12 text-center text-slate-500">
                  {emptyMessage}
                </TableCell>
              </TableRow>
            ) : (
              data.map((row, index) => (
                <TableRow
                  key={getRowKey(row)}
                  onClick={() => onRowClick?.(row)}
                  className={cn(onRowClick && 'cursor-pointer')}
                >
                  {columns.map((column) => (
                    <TableCell
                      key={column.key}
                      className={cn(isActionsColumn(column) && rowActionsCellClassName, column.cellClassName)}
                    >
                      {column.render
                        ? column.render(row, index)
                        : String(getCellValue(row, column) ?? '')}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <DataTablePagination
        pageNumber={request.pageNumber}
        pageSize={request.pageSize ?? defaultPageSize}
        totalCount={totalCount}
        pageCount={pageCount}
        pageSizeOptions={pageSizeOptions}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  )
}
