import { useCallback, useEffect, useState } from 'react'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Modal } from '@/Components/ui'
import { listProductionOrders } from '@/Requests/productionOrders'
import { createDefaultPaginationRequest } from '@/helpers/api/pagination'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { isReleasedProductionOrder } from '@/helpers/productionOrderMapper'
import type { ProductionOrder } from '@/types/production'

interface ProductionOrderSelectionDialogProps {
  isOpen: boolean
  onClose: () => void
  onSelected: (order: ProductionOrder) => void | Promise<void>
}

export function ProductionOrderSelectionDialog({ isOpen, onClose, onSelected }: ProductionOrderSelectionDialogProps) {
  const [rows, setRows] = useState<ProductionOrder[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<ProductionOrder | null>(null)
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    if (!isOpen) return
    setLoading(true)
    setError(null)
    setSelected(null)
    listProductionOrders(createDefaultPaginationRequest({ pageSize: 100 }))
      .then((res) => setRows((res.data ?? []).filter(isReleasedProductionOrder)))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load production orders'))
      .finally(() => setLoading(false))
  }, [isOpen])

  const isSelected = useCallback(
    (row: ProductionOrder) => selected?.AbsoluteEntry != null && selected.AbsoluteEntry === row.AbsoluteEntry,
    [selected],
  )

  const columns: SapColumn<ProductionOrder>[] = [
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
    { key: 'DocumentNumber', header: 'Production Order No.', accessor: (r) => r.DocumentNumber },
    { key: 'Status', header: 'Status', accessor: (r) => r.Status ?? '—' },
    {
      key: 'Project',
      header: 'Project',
      accessor: (r) => formatCodeWithName(r.Project, r.ProjectName),
    },
    {
      key: 'ItemNumber',
      header: 'Product',
      accessor: (r) => formatCodeWithName(r.ItemNumber, r.ProductDescription),
    },
    { key: 'DrawingNo', header: 'Drawing No.', accessor: (r) => r.DrawingNo },
  ]

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

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Select Production Order"
      size="full"
      footer={(
        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={onClose} disabled={confirming}>Cancel</Button>
          <Button onClick={handleConfirm} disabled={!selected || confirming} isLoading={confirming}>
            Select Production Order
          </Button>
        </div>
      )}
    >
      {error && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}
      <SapDataGrid
        columns={columns}
        data={rows}
        loading={loading}
        getRowKey={(row) => row.AbsoluteEntry ?? row.DocumentNumber ?? Math.random()}
        emptyMessage="No released production orders found"
        onRowClick={setSelected}
        isRowSelected={isSelected}
        maxHeight="55vh"
      />
    </Modal>
  )
}
