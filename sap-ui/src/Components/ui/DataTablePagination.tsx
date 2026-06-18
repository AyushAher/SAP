import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import { DEFAULT_PAGE_SIZE_OPTIONS } from '@/helpers/api/pagination'
import { Button } from './Button'
import { Select } from './Select'

export interface DataTablePaginationProps {
  pageNumber: number
  pageSize: number
  totalCount: number
  pageCount: number
  pageSizeOptions?: number[]
  onPageChange: (page: number) => void
  onPageSizeChange: (size: number) => void
  className?: string
}

function getPageRange(current: number, total: number): (number | 'ellipsis')[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1)
  }

  const pages: (number | 'ellipsis')[] = [1]

  if (current > 3) pages.push('ellipsis')

  const start = Math.max(2, current - 1)
  const end = Math.min(total - 1, current + 1)

  for (let i = start; i <= end; i++) {
    pages.push(i)
  }

  if (current < total - 2) pages.push('ellipsis')
  pages.push(total)

  return pages
}

export function DataTablePagination({
  pageNumber,
  pageSize,
  totalCount,
  pageCount,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  onPageChange,
  onPageSizeChange,
  className,
}: DataTablePaginationProps) {
  const start = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1
  const end = Math.min(pageNumber * pageSize, totalCount)
  const pages = getPageRange(pageNumber, pageCount)

  const sizeOptions = pageSizeOptions.map((size) => ({
    value: String(size),
    label: `${size} per page`,
  }))

  return (
    <div
      className={cn(
        'flex flex-col gap-4 border-t border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between',
        className,
      )}
    >
      <div className="flex items-center gap-4">
        <p className="text-sm text-slate-600">
          Showing <span className="font-medium">{start}</span> to{' '}
          <span className="font-medium">{end}</span> of{' '}
          <span className="font-medium">{totalCount}</span> results
        </p>
        <div className="w-36">
          <Select
            options={sizeOptions}
            value={String(pageSize)}
            onChange={(value) => onPageSizeChange(Number(value))}
          />
        </div>
      </div>

      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(1)}
          disabled={pageNumber <= 1}
          aria-label="First page"
        >
          <ChevronsLeft className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(pageNumber - 1)}
          disabled={pageNumber <= 1}
          aria-label="Previous page"
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>

        <div className="hidden items-center gap-1 sm:flex">
          {pages.map((page, index) =>
            page === 'ellipsis' ? (
              <span key={`ellipsis-${index}`} className="px-2 text-slate-400">
                ...
              </span>
            ) : (
              <Button
                key={page}
                variant={page === pageNumber ? 'primary' : 'outline'}
                size="sm"
                onClick={() => onPageChange(page)}
                className="min-w-[36px]"
              >
                {page}
              </Button>
            ),
          )}
        </div>

        <span className="px-2 text-sm text-slate-600 sm:hidden">
          {pageNumber} / {pageCount}
        </span>

        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(pageNumber + 1)}
          disabled={pageNumber >= pageCount}
          aria-label="Next page"
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(pageCount)}
          disabled={pageNumber >= pageCount}
          aria-label="Last page"
        >
          <ChevronsRight className="h-4 w-4" />
        </Button>
      </div>
    </div>
  )
}
