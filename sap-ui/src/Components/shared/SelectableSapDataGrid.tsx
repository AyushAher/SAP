import { useMemo, useState } from 'react'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { Button } from '@/Components/ui'

interface SelectableSapDataGridProps<T> {
  columns: SapColumn<T>[]
  data: T[]
  loading?: boolean
  emptyMessage?: string
  getRowKey: (row: T) => string | number
  onRemoveSelected?: (rows: T[]) => void
  toolbarTitle?: string
}

export function SelectableSapDataGrid<T>({
  columns,
  data,
  loading,
  emptyMessage,
  getRowKey,
  onRemoveSelected,
  toolbarTitle,
}: SelectableSapDataGridProps<T>) {
  const [selectedKeys, setSelectedKeys] = useState<Array<string | number>>([])

  const selectedRows = useMemo(
    () => data.filter((row) => selectedKeys.includes(getRowKey(row))),
    [data, getRowKey, selectedKeys],
  )

  const selectionColumn: SapColumn<T> = {
    key: '__select',
    header: '',
    render: (row) => {
      const key = getRowKey(row)
      return (
        <input
          type="checkbox"
          checked={selectedKeys.includes(key)}
          onChange={(e) => {
            setSelectedKeys((prev) =>
              e.target.checked ? [...prev, key] : prev.filter((k) => k !== key),
            )
          }}
        />
      )
    },
  }

  return (
    <div className="space-y-3">
      {(toolbarTitle || onRemoveSelected) && (
        <div className="flex items-center justify-between gap-3">
          {toolbarTitle && <h3 className="text-sm font-semibold text-slate-800">{toolbarTitle}</h3>}
          {onRemoveSelected && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={!selectedRows.length}
              onClick={() => {
                onRemoveSelected(selectedRows)
                setSelectedKeys([])
              }}
            >
              Remove Selected
            </Button>
          )}
        </div>
      )}
      <SapDataGrid
        columns={[selectionColumn, ...columns]}
        data={data}
        loading={loading}
        emptyMessage={emptyMessage}
        getRowKey={getRowKey}
      />
    </div>
  )
}
