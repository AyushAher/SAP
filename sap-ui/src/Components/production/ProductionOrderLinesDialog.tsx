import { useCallback, useEffect, useMemo, useState } from 'react'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Modal } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { normalizeProductionOrderLine } from '@/helpers/productionOrderMapper'
import { getProductionOrderLines } from '@/Requests/productionOrders'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

interface ProductionOrderLinesDialogProps {
  isOpen: boolean
  order?: ProductionOrder | null
  onClose: () => void
  onConfirm: (lines: ProductionOrderLine[]) => void
}

function lineKey(line: ProductionOrderLine) {
  return `${line.LineNumber}-${line.ItemNo}`
}

export function ProductionOrderLinesDialog({ isOpen, order, onClose, onConfirm }: ProductionOrderLinesDialogProps) {
  const [selected, setSelected] = useState<ProductionOrderLine[]>([])
  const [lines, setLines] = useState<ProductionOrderLine[]>([])
  const [loadingLines, setLoadingLines] = useState(false)
  const [linesError, setLinesError] = useState<string | null>(null)

  const itemCodes = useMemo(() => lines.map((line) => line.ItemNo), [lines])
  const itemMasterMap = useItemMasterMap(itemCodes)

  useEffect(() => {
    if (!isOpen) {
      setSelected([])
      setLines([])
      setLinesError(null)
      return
    }

    if (!order?.AbsoluteEntry) {
      setLines([])
      return
    }

    const embedded = order.ProductionOrderLines ?? []
    if (embedded.length > 0) {
      setLines(embedded.map(normalizeProductionOrderLine))
      return
    }

    setLoadingLines(true)
    setLinesError(null)
    getProductionOrderLines(String(order.AbsoluteEntry))
      .then((res) => {
        const raw = res?.Value ?? res?.value ?? []
        setLines(raw.map(normalizeProductionOrderLine))
      })
      .catch((err) => {
        setLinesError(err instanceof Error ? err.message : 'Failed to load production order lines')
        setLines([])
      })
      .finally(() => setLoadingLines(false))
  }, [isOpen, order])

  const isLineSelected = useCallback(
    (line: ProductionOrderLine) => selected.some((x) => lineKey(x) === lineKey(line)),
    [selected],
  )

  const toggleLine = useCallback((line: ProductionOrderLine) => {
    setSelected((prev) => {
      const key = lineKey(line)
      const exists = prev.some((x) => lineKey(x) === key)
      return exists ? prev.filter((x) => lineKey(x) !== key) : [...prev, line]
    })
  }, [])

  const columns: SapColumn<ProductionOrderLine>[] = [
    {
      key: 'select',
      header: '',
      render: (row) => (
        <input
          type="checkbox"
          checked={isLineSelected(row)}
          onChange={() => toggleLine(row)}
          onClick={(e) => e.stopPropagation()}
        />
      ),
    },
    { key: 'sr', header: 'Sr. No.', accessor: (r) => (r.VisualOrder ?? 0) + 1 },
    { key: 'LineNumber', header: 'Line No.', accessor: (r) => r.LineNumber },
    {
      key: 'ItemNo',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemNo, r.ItemName ?? itemMasterMap[r.ItemNo ?? '']?.name),
    },
    { key: 'IssuedQuantity', header: 'Issued Qty', accessor: (r) => r.IssuedQuantity },
    { key: 'PlannedQuantity', header: 'Planned Qty', accessor: (r) => r.PlannedQuantity },
    { key: 'Warehouse', header: 'Issuing Warehouse', accessor: (r) => r.Warehouse },
    { key: 'uom', header: 'Stock Unit', accessor: (r) => (r.ItemNo ? itemMasterMap[r.ItemNo]?.uom ?? '' : '') },
  ]

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={`Select Production Order Items - ${order?.DocumentNumber ?? ''}`}
      size="full"
      footer={(
        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={() => onConfirm(selected)} disabled={!selected.length}>Select Lines</Button>
        </div>
      )}
    >
      {linesError && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{linesError}</div>
      )}
      <SapDataGrid
        columns={columns}
        data={lines}
        loading={loadingLines}
        getRowKey={(row) => lineKey(row)}
        emptyMessage="No production order lines available"
        onRowClick={toggleLine}
        isRowSelected={isLineSelected}
        maxHeight="55vh"
      />
    </Modal>
  )
}
