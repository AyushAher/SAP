import { useCallback, useEffect, useMemo, useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { DataTable, type DataTableColumn } from '@/Components/ui'
import { Button, Modal } from '@/Components/ui'
import { RowActionButton, rowActionIconClassName } from '@/Components/shared/RowActions'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useDocumentSync } from '@/hooks/useDocumentSync'
import { listProductionOrders, syncProductionOrderFromSap } from '@/Requests/productionOrders'
import type { PaginationRequest } from '@/types/api'
import type { ProductionOrder } from '@/types/production'

interface ProductionOrderSelectionDialogProps {
  isOpen: boolean
  onClose: () => void
  onSelected: (order: ProductionOrder) => void | Promise<void>
}

const RELEASED_STATUS = 'boposReleased'

export function ProductionOrderSelectionDialog({ isOpen, onClose, onSelected }: ProductionOrderSelectionDialogProps) {
  const [selected, setSelected] = useState<ProductionOrder | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isOpen) return
    setSelected(null)
    setError(null)
  }, [isOpen])

  // The released filter is applied server-side against the local mirror, so every released
  // production order is reachable through paging — not just the ones on the first SAP page.
  // Kept dependency-free so the table never refetches in a loop.
  const fetchData = useCallback(async (request: PaginationRequest) => {
    const filters = [
      { field: 'Status', operator: 'eq' as const, value: RELEASED_STATUS },
      ...(request.filters ?? []).filter((f) => f.field.toLowerCase() !== 'status'),
    ]
    return listProductionOrders({
      ...request,
      filters,
      includeTotalCount: true,
    })
  }, [])

  const { tableKey, syncingKey, syncError, handleSyncRow } = useDocumentSync({
    syncRow: syncProductionOrderFromSap,
  })

  const isSelected = useCallback(
    (row: ProductionOrder) => selected?.AbsoluteEntry != null && selected.AbsoluteEntry === row.AbsoluteEntry,
    [selected],
  )

  const columns = useMemo<DataTableColumn<ProductionOrder>[]>(() => [
    {
      key: 'select',
      header: '',
      render: (row) => (
        <input
          type="radio"
          name="production-order"
          checked={isSelected(row)}
          onChange={() => setSelected(row)}
          onClick={(e) => e.stopPropagation()}
        />
      ),
    },
    {
      key: 'DocumentNumber',
      header: 'Production Order No.',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.DocumentNumber,
    },
    {
      key: 'Status',
      header: 'Status',
      sortable: true,
      accessor: (r) => r.Status ?? '—',
    },
    {
      key: 'SalesOrderDocNum',
      header: 'Sales Order',
      sortable: true,
      filterable: true,
      accessor: (r) => r.SalesOrderDocNum ?? '—',
    },
    {
      key: 'Project',
      header: 'Project',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.Project,
    },
    {
      key: 'ProjectName',
      header: 'Project Name',
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.ProjectName || '—',
    },
    {
      key: 'CustomerName',
      header: 'Business Partner Name',
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.CustomerName || r.CustomerCode || '—',
    },
    {
      key: 'ItemNumber',
      header: 'Product',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => formatCodeWithName(r.ItemNumber, r.ProductDescription),
    },
    {
      key: 'Warehouse',
      header: 'Warehouse',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.Warehouse || '—',
    },
    {
      key: 'DrawingNo',
      header: 'Drawing No.',
      sortable: true,
      filterable: true,
      filterOperator: 'contains',
      accessor: (r) => r.DrawingNo,
    },
    {
      key: 'sync',
      header: '',
      render: (row) => {
        const absoluteEntry = row.AbsoluteEntry
        const rowBusy = absoluteEntry != null && syncingKey === absoluteEntry
        return (
          <RowActionButton
            title="Sync from SAP"
            disabled={absoluteEntry == null || rowBusy}
            icon={<RefreshCw className={`${rowActionIconClassName}${rowBusy ? ' animate-spin' : ''}`} />}
            onClick={(e) => {
              e.stopPropagation()
              if (absoluteEntry != null) void handleSyncRow(absoluteEntry)
            }}
          />
        )
      },
    },
  ], [handleSyncRow, isSelected, syncingKey])

  const handleConfirm = useCallback(async () => {
    if (!selected?.AbsoluteEntry) return
    setConfirming(true)
    setError(null)
    try {
      await onSelected(selected)
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load production order')
    } finally {
      setConfirming(false)
    }
  }, [onClose, onSelected, selected])

  const handleClose = useCallback(() => {
    setSelected(null)
    setError(null)
    onClose()
  }, [onClose])

  const visibleError = error ?? syncError

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title="Select Production Order"
      size="full"
      footer={(
        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={handleClose} disabled={confirming}>Cancel</Button>
          <Button onClick={handleConfirm} disabled={!selected || confirming} isLoading={confirming}>
            Select Production Order
          </Button>
        </div>
      )}
    >
      {visibleError && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{visibleError}</div>
      )}
      {isOpen && (
        <DataTable
          key={tableKey}
          columns={columns}
          fetchData={fetchData}
          getRowKey={(row) => row.AbsoluteEntry ?? row.DocumentNumber ?? `${row.Project ?? ''}-${row.ItemNumber ?? ''}`}
          onRowClick={setSelected}
          emptyMessage="No released production orders found"
          defaultPageSize={20}
        />
      )}
    </Modal>
  )
}
