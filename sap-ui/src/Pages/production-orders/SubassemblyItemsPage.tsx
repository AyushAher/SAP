import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Button, Card, CardContent, Input, SearchableSelect } from '@/Components/ui'
import { productionOrderFormPath, productionOrderSubassemblyPath } from '@/config/constants'
import { formatSubassemblyNo, validateSubassemblyItemsForm } from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { getProductionOrder, updateProductionOrder } from '@/Requests/productionOrders'
import { searchItems } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

export function SubassemblyItemsPage() {
  const { id, childId } = useParams()
  const navigate = useNavigate()
  const [parent, setParent] = useState<ProductionOrder | null>(null)
  const [child, setChild] = useState<ProductionOrder | null>(null)
  const [lines, setLines] = useState<ProductionOrderLine[]>([])
  const [draft, setDraft] = useState<ProductionOrderLine>({ ItemNo: '', PlannedQuantity: 0 })
  const [draftLabel, setDraftLabel] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id || !childId) return
    let cancelled = false
    setLoading(true)
    void (async () => {
      try {
        const [parentOrder, childOrder] = await Promise.all([
          getProductionOrder(id),
          getProductionOrder(childId),
        ])
        if (cancelled) return
        setParent(parentOrder)
        setChild(childOrder)
        setLines(childOrder.ProductionOrderLines ?? [])
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load sub-assembly items.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [id, childId])

  const itemCodes = useMemo(
    () => [...lines.map((line) => line.ItemNo), draft.ItemNo],
    [lines, draft.ItemNo],
  )
  const itemMap = useItemMasterMap(itemCodes)

  const searchItemOptions = async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(search)
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }

  const handleAdd = () => {
    if (!draft.ItemNo) {
      setError('Item Code is required.')
      return
    }
    if ((draft.PlannedQuantity ?? 0) <= 0) {
      setError('Qty must be greater than zero.')
      return
    }
    const nextNumber = Math.max(0, ...lines.map((line) => line.LineNumber ?? 0)) + 1
    const details = itemMap[draft.ItemNo]
    setLines([
      ...lines,
      {
        ...draft,
        LineNumber: nextNumber,
        ItemName: draft.ItemName || details?.name,
        Warehouse: child?.Warehouse || parent?.Warehouse,
      },
    ])
    setDraft({ ItemNo: '', PlannedQuantity: 0 })
    setDraftLabel('')
    setError(null)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (!child?.AbsoluteEntry) return
    const message = validateSubassemblyItemsForm(lines)
    if (message) {
      setError(message)
      toast.error(message)
      return
    }
    setSaving(true)
    setError(null)
    try {
      await updateProductionOrder(child.AbsoluteEntry, { ...child, ProductionOrderLines: lines })
      toast.success('Sub-assembly items updated.')
      navigate(productionOrderFormPath(id!))
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Failed to save items.'
      setError(text)
      toast.error(text)
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  return (
    <div className="space-y-6">
      <PageHeader
        title="Add Items in Sub-assembly"
        description="Component items for this sub-assembly production order."
      />
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {error}
        </div>
      )}
      <Card>
        <CardContent className="space-y-6 pt-6">
          <div className="grid gap-4 md:grid-cols-4 text-sm">
            <div>
              <div className="text-slate-500">Order No.</div>
              <div className="font-medium text-slate-900">{parent?.DocumentNumber ?? '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Subassembly No.</div>
              <div className="font-medium text-slate-900">
                {formatSubassemblyNo(child ?? {}, parent?.DocumentNumber) || '—'}
              </div>
            </div>
            <div>
              <div className="text-slate-500">Drawing No.</div>
              <div className="font-medium text-slate-900">{child?.DrawingNo || '—'}</div>
            </div>
            <div>
              <div className="text-slate-500">Drawing Name</div>
              <div className="font-medium text-slate-900">{child?.ProductDescription || '—'}</div>
            </div>
          </div>

          <form className="space-y-6" onSubmit={(e) => void handleSubmit(e)}>
            <div className="grid gap-4 md:grid-cols-5">
              <SearchableSelect
                label="Item Code"
                lookupKind="item"
                value={draft.ItemNo ?? ''}
                selectedLabel={draftLabel}
                placeholder="Search item..."
                onSearch={searchItemOptions}
                onChange={(code, option) => {
                  const label = option?.label ?? code
                  const name = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : undefined
                  setDraftLabel(label)
                  setDraft({
                    ...draft,
                    ItemNo: code,
                    ItemName: name,
                  })
                }}
              />
              <Input label="Item Name" value={draft.ItemName || itemMap[draft.ItemNo ?? '']?.name || ''} disabled />
              <Input
                label="Qty"
                type="number"
                nonNegative
                value={String(draft.PlannedQuantity ?? 0)}
                onChange={(e) => setDraft({ ...draft, PlannedQuantity: Number(e.target.value) })}
              />
              <Input
                label="Stock UoM"
                value={itemMap[draft.ItemNo ?? '']?.stockUom || ''}
                disabled
              />
              <div className="flex items-end">
                <Button type="button" variant="outline" onClick={handleAdd}>Add item</Button>
              </div>
            </div>

            <SapDataGrid
              data={lines}
              getRowKey={(row) => row.LineNumber ?? lines.indexOf(row)}
              emptyMessage="No items yet. Add an item above."
              columns={[
                {
                  key: 'sr',
                  header: 'Sr. No.',
                  accessor: (row) => lines.indexOf(row) + 1,
                },
                { key: 'ItemNo', header: 'Item Code', accessor: (row) => row.ItemNo ?? '—' },
                {
                  key: 'ItemName',
                  header: 'Item Name',
                  accessor: (row) => row.ItemName || itemMap[row.ItemNo ?? '']?.name || '—',
                },
                { key: 'PlannedQuantity', header: 'Qty', accessor: (row) => row.PlannedQuantity ?? 0 },
                {
                  key: 'Uom',
                  header: 'Stock UoM',
                  accessor: (row) => itemMap[row.ItemNo ?? '']?.stockUom || '—',
                },
              ]}
              actions={(row) => (
                <RowActions>
                  <RowActionButton
                    title="Delete item"
                    variant="danger"
                    icon={<Trash2 className={rowActionIconClassName} />}
                    onClick={() => setLines(lines.filter((line) => line !== row))}
                  />
                </RowActions>
              )}
            />

            <div className="flex gap-3">
              <Button type="submit" isLoading={saving}>{child?.ProductionOrderLines?.length ? 'Update' : 'Add'}</Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => navigate(productionOrderSubassemblyPath(id!, childId))}
              >
                Back
              </Button>
              <Button type="button" variant="outline" onClick={() => navigate(productionOrderFormPath(id!))}>
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
