import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { Button, Card, CardContent, CardHeader, CardTitle } from '@/Components/ui'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import {
  ROUTES,
  productionOrderSubassemblyPath,
} from '@/config/constants'
import { subassemblyStatusLabel, formatSubassemblyNo, issuedQuantityTotal } from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import {
  cancelProductionOrder,
  listSubassemblies,
  type ProductionOrder,
} from '@/Requests/productionOrders'

interface ProductionOrderSubassembliesCardProps {
  parent: ProductionOrder
  /** Local sub-assemblies while the parent is still a draft. */
  draftRows?: ProductionOrder[]
  /** Open the sub-assembly form without creating the parent in SAP. */
  onAddUnsaved?: () => void
  onEditDraft?: (row: ProductionOrder) => void
  onDeleteDraft?: (row: ProductionOrder) => void
  adding?: boolean
}

export function ProductionOrderSubassembliesCard({
  parent,
  draftRows,
  onAddUnsaved,
  onEditDraft,
  onDeleteDraft,
  adding = false,
}: ProductionOrderSubassembliesCardProps) {
  const parentId = parent.AbsoluteEntry
  const saved = parentId != null && parent.DocumentNumber != null
  const [rows, setRows] = useState<ProductionOrder[]>(draftRows ?? [])
  const [loading, setLoading] = useState(saved)
  const [error, setError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<number | string | null>(null)

  const reload = useCallback(async () => {
    if (parentId == null) return
    setLoading(true)
    setError(null)
    try {
      setRows(await listSubassemblies(parentId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load sub-assemblies.')
    } finally {
      setLoading(false)
    }
  }, [parentId])

  useEffect(() => {
    if (!saved) return
    void reload()
  }, [saved, reload])

  useEffect(() => {
    if (saved) return
    setRows(draftRows ?? [])
    setLoading(false)
  }, [saved, draftRows])

  const handleDelete = async (row: ProductionOrder) => {
    if (!saved) {
      if (!row.DraftKey) return
      if (!window.confirm(`Remove sub-assembly ${formatSubassemblyNo(row, parent.DocumentNumber, rows)}? It has not been saved to SAP yet.`)) {
        return
      }
      onDeleteDraft?.(row)
      return
    }
    if (row.AbsoluteEntry == null) return
    if (!window.confirm(`Cancel sub-assembly ${formatSubassemblyNo(row, parent.DocumentNumber, rows) || row.AbsoluteEntry}? This cannot be undone in ConnectEdge.`)) {
      return
    }
    setDeletingId(row.AbsoluteEntry)
    setError(null)
    try {
      await cancelProductionOrder(row.AbsoluteEntry)
      toast.success('Sub-assembly cancelled.')
      await reload()
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to cancel sub-assembly.'
      setError(message)
      toast.error(message)
    } finally {
      setDeletingId(null)
    }
  }

  const addHref = saved
    ? productionOrderSubassemblyPath(parentId!)
    : productionOrderSubassemblyPath(ROUTES.PRODUCTION_ORDER_DRAFT_ID)

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle>Sub-assemblies</CardTitle>
        {saved ? (
          <Link to={addHref}>
            <Button type="button" size="sm" leftIcon={<Plus className="h-4 w-4" />}>Add Sub-assembly</Button>
          </Link>
        ) : (
          <Button
            type="button"
            size="sm"
            isLoading={adding}
            leftIcon={<Plus className="h-4 w-4" />}
            onClick={() => onAddUnsaved?.()}
          >
            Add Sub-assembly
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-3">
        {!saved && (
          <p className="text-sm text-slate-500">
            Add sub-assemblies and their items here. They are created in SAP when you save this production order.
          </p>
        )}
        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
        )}
        <SapDataGrid
          loading={loading}
          data={rows}
          getRowKey={(row) => row.DraftKey ?? row.AbsoluteEntry ?? 0}
          emptyMessage={saved ? 'No sub-assemblies yet.' : 'No sub-assemblies yet. Add one, then save the production order.'}
          columns={[
            { key: 'DocumentNumber', header: 'Subassembly No.', accessor: (r) => formatSubassemblyNo(r, parent.DocumentNumber, rows) || '—' },
            { key: 'DrawingNo', header: 'Drawing No.', accessor: (r) => r.DrawingNo || '—' },
            { key: 'ProductDescription', header: 'Drawing Name', accessor: (r) => r.ProductDescription || '—' },
            { key: 'Weight', header: 'Weight', accessor: (r) => r.Weight ?? '—' },
            { key: 'PlannedQuantity', header: 'Qty', accessor: (r) => r.PlannedQuantity ?? 0 },
            { key: 'IssuedQuantity', header: 'Issued Qty', accessor: (r) => issuedQuantityTotal(r) },
            { key: 'Status', header: 'Status', accessor: (r) => subassemblyStatusLabel(r.Status) },
          ]}
              actions={(row) => (
                <RowActions>
                  {saved ? (
                    <RowActionLink
                      to={productionOrderSubassemblyPath(parentId!, row.AbsoluteEntry)}
                      title="Edit sub-assembly"
                      icon={<Pencil className={rowActionIconClassName} />}
                    />
                  ) : (
                    <RowActionButton
                      title="Edit sub-assembly"
                      icon={<Pencil className={rowActionIconClassName} />}
                      onClick={() => onEditDraft?.(row)}
                    />
                  )}
              <RowActionButton
                title="Delete sub-assembly"
                variant="danger"
                disabled={deletingId === (row.DraftKey ?? row.AbsoluteEntry)}
                icon={<Trash2 className={rowActionIconClassName} />}
                onClick={() => void handleDelete(row)}
              />
            </RowActions>
          )}
        />
      </CardContent>
    </Card>
  )
}
