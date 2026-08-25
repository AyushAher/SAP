import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { Button, Card, CardContent, CardHeader, CardTitle } from '@/Components/ui'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import {
  productionOrderSubassemblyItemsPath,
  productionOrderSubassemblyPath,
} from '@/config/constants'
import { productionOrderStatusLabel } from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import {
  cancelProductionOrder,
  listSubassemblies,
  type ProductionOrder,
} from '@/Requests/productionOrders'

interface ProductionOrderSubassembliesCardProps {
  parent: ProductionOrder
}

export function ProductionOrderSubassembliesCard({ parent }: ProductionOrderSubassembliesCardProps) {
  const parentId = parent.AbsoluteEntry
  const saved = parentId != null && parent.DocumentNumber != null
  const [rows, setRows] = useState<ProductionOrder[]>([])
  const [loading, setLoading] = useState(saved)
  const [error, setError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<number | null>(null)

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

  const handleDelete = async (row: ProductionOrder) => {
    if (row.AbsoluteEntry == null) return
    if (!window.confirm(`Cancel sub-assembly ${row.DocumentNumber ?? row.AbsoluteEntry}? This cannot be undone in ConnectEdge.`)) {
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

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <CardTitle>Sub-assemblies</CardTitle>
        {saved ? (
          <Link to={productionOrderSubassemblyPath(parentId!)}>
            <Button type="button" size="sm" leftIcon={<Plus className="h-4 w-4" />}>Add Sub-assembly</Button>
          </Link>
        ) : null}
      </CardHeader>
      <CardContent className="space-y-3">
        {!saved && (
          <p className="text-sm text-slate-500">Save this production order in SAP before adding sub-assemblies.</p>
        )}
        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
        )}
        {saved && (
          <SapDataGrid
            loading={loading}
            data={rows}
            getRowKey={(row) => row.AbsoluteEntry ?? 0}
            emptyMessage="No sub-assemblies yet."
            columns={[
              { key: 'DocumentNumber', header: 'Subassembly No.', accessor: (r) => r.DocumentNumber ?? '—' },
              { key: 'ItemNumber', header: 'Product', accessor: (r) => r.ItemNumber ?? '—' },
              { key: 'DrawingNo', header: 'Drawing No.', accessor: (r) => r.DrawingNo || '—' },
              { key: 'ProductDescription', header: 'Drawing Name', accessor: (r) => r.ProductDescription || '—' },
              { key: 'PlannedQuantity', header: 'Qty', accessor: (r) => r.PlannedQuantity ?? 0 },
              { key: 'Status', header: 'Status', accessor: (r) => productionOrderStatusLabel(r.Status) },
            ]}
            actions={(row) => (
              <RowActions>
                <RowActionLink
                  to={productionOrderSubassemblyPath(parentId!, row.AbsoluteEntry)}
                  title="Edit sub-assembly"
                  icon={<Pencil className={rowActionIconClassName} />}
                />
                <RowActionLink
                  to={productionOrderSubassemblyItemsPath(parentId!, row.AbsoluteEntry!)}
                  title="Edit items"
                  icon={<Plus className={rowActionIconClassName} />}
                />
                <RowActionButton
                  title="Delete sub-assembly"
                  variant="danger"
                  disabled={deletingId === row.AbsoluteEntry}
                  icon={<Trash2 className={rowActionIconClassName} />}
                  onClick={() => void handleDelete(row)}
                />
              </RowActions>
            )}
          />
        )}
      </CardContent>
    </Card>
  )
}
