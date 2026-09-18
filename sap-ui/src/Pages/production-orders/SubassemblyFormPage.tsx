import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SapDataGrid } from '@/Components/shared/SapDataGrid'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { Button, Card, CardContent, Input, SearchableSelect, Select, Textarea } from '@/Components/ui'
import { productionOrderFormPath, ROUTES } from '@/config/constants'
import {
  buildSubassemblyDraftFromParent,
  buildSubassemblyItemLine,
  formatSubassemblyNo,
  SUBASSEMBLY_STATUS_OPTIONS,
  subassemblyDrawingName,
  subassemblyStatusLabel,
  validateSubassemblyForm,
} from '@/helpers/productionOrderForm'
import {
  loadCreateDraft,
  newDraftSubassemblyKey,
  upsertDraftSubassembly,
} from '@/helpers/productionOrderCreateDraft'
import { ensureItemList, searchItemsCached } from '@/helpers/itemSearchCache'
import { toast } from '@/helpers/toast'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import {
  createProductionOrder,
  getProductionOrder,
  listSubassemblies,
  updateProductionOrder,
} from '@/Requests/productionOrders'
import type { SelectOption } from '@/types'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

function stampHeaderOntoLines(
  rows: ProductionOrderLine[],
  header: { drawingNo?: string; drawingName?: string; status?: string },
): ProductionOrderLine[] {
  return rows.map((line) => ({
    ...line,
    DrawingNo: header.drawingNo || line.DrawingNo,
    DrawingName: header.drawingName || line.DrawingName,
    Status: header.status || line.Status,
  }))
}

export function SubassemblyFormPage() {
  const { id, childId } = useParams()
  const navigate = useNavigate()
  const isDraft = id === ROUTES.PRODUCTION_ORDER_DRAFT_ID
  const parentFormPath = isDraft ? ROUTES.PRODUCTION_ORDER_FORM : productionOrderFormPath(id!)
  const [parent, setParent] = useState<ProductionOrder | null>(null)
  const [form, setForm] = useState<ProductionOrder>({})
  const [drawingName, setDrawingName] = useState('')
  const [lines, setLines] = useState<ProductionOrderLine[]>([])
  const [draft, setDraft] = useState<ProductionOrderLine>({ ItemNo: '', PlannedQuantity: 0, Status: 'boposPlanned' })
  const [draftLabel, setDraftLabel] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void ensureItemList().catch(() => undefined)
  }, [])

  useEffect(() => {
    if (!id) return
    let cancelled = false
    setLoading(true)
    void (async () => {
      try {
        if (isDraft) {
          const stored = loadCreateDraft()
          if (!stored?.header.ItemNumber) {
            throw new Error('Finish the production order header before adding a sub-assembly.')
          }
          const parentOrder = stored.header
          const siblings = stored.subassemblies
          if (cancelled) return
          setParent(parentOrder)
          if (childId) {
            const child = siblings.find((row) => row.DraftKey === childId)
            if (!child) throw new Error('That sub-assembly is no longer on this draft.')
            const name = subassemblyDrawingName(child, parentOrder)
            setForm({
              ...child,
              ItemNumber: parentOrder.ItemNumber ?? child.ItemNumber,
              Warehouse: child.Warehouse || parentOrder.Warehouse,
              IssWarehouse: child.IssWarehouse || parentOrder.IssWarehouse,
              Status: child.Status || 'boposPlanned',
            })
            setDrawingName(name)
            setDraft((current) => ({ ...current, Status: child.Status || 'boposPlanned' }))
            setLines(stampHeaderOntoLines(child.ProductionOrderLines ?? [], {
              drawingNo: child.DrawingNo,
              drawingName: name,
              status: child.Status || 'boposPlanned',
            }))
          } else {
            const next = buildSubassemblyDraftFromParent(parentOrder, siblings)
            setForm({ ...next, DraftKey: newDraftSubassemblyKey(siblings) })
            setDrawingName('')
            setDraft((current) => ({ ...current, Status: next.Status || 'boposPlanned' }))
            setLines([])
          }
          return
        }

        const [parentOrder, siblings] = await Promise.all([
          getProductionOrder(id),
          listSubassemblies(id, { includeCancelled: true }),
        ])
        if (cancelled) return
        setParent(parentOrder)
        if (childId) {
          const child = await getProductionOrder(childId)
          if (cancelled) return
          const name = subassemblyDrawingName(child, parentOrder)
          const status = child.Status || 'boposPlanned'
          setForm({
            ...child,
            ItemNumber: parentOrder.ItemNumber ?? child.ItemNumber,
            ParentProductionOrderNo: parentOrder.DocumentNumber != null
              ? formatSubassemblyNo(child, parentOrder.DocumentNumber)
              : child.ParentProductionOrderNo,
            ParentAbsoluteEntry: child.ParentAbsoluteEntry ?? parentOrder.AbsoluteEntry,
            Warehouse: child.Warehouse || parentOrder.Warehouse,
            IssWarehouse: child.IssWarehouse || parentOrder.IssWarehouse,
            Project: child.Project || parentOrder.Project,
            ProjectName: child.ProjectName || parentOrder.ProjectName,
            Status: status,
          })
          setDrawingName(name)
          setDraft((current) => ({ ...current, Status: status }))
          setLines(stampHeaderOntoLines(child.ProductionOrderLines ?? [], {
            drawingNo: child.DrawingNo,
            drawingName: name,
            status,
          }))
        } else {
          const next = buildSubassemblyDraftFromParent(parentOrder, siblings)
          setForm(next)
          setDrawingName('')
          setDraft((current) => ({ ...current, Status: next.Status || 'boposPlanned' }))
          setLines([])
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load sub-assembly.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [id, childId, isDraft])

  const itemCodes = useMemo(
    () => [...lines.map((line) => line.ItemNo), draft.ItemNo],
    [lines, draft.ItemNo],
  )
  const itemMap = useItemMasterMap(itemCodes)

  const searchItemOptions = async (search: string): Promise<SelectOption[]> => {
    const response = await searchItemsCached(search)
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
    const details = itemMap[draft.ItemNo]
    setLines([
      ...lines,
      buildSubassemblyItemLine(
        {
          ...draft,
          ItemName: draft.ItemName || details?.name,
          DrawingNo: draft.DrawingNo || form.DrawingNo,
          DrawingName: draft.DrawingName || drawingName,
          Status: draft.Status || form.Status || 'boposPlanned',
        },
        { ...form, ProductDescription: drawingName, DrawingNo: form.DrawingNo },
        parent,
        lines,
        { drawingName, status: form.Status || 'boposPlanned' },
      ),
    ])
    setDraft({ ItemNo: '', PlannedQuantity: 0, FreeText: '', Status: form.Status || 'boposPlanned' })
    setDraftLabel('')
    setError(null)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const status = form.Status || 'boposPlanned'
    const payload: ProductionOrder = {
      ...form,
      Status: status,
      ItemNumber: parent?.ItemNumber ?? form.ItemNumber,
      ProductDescription: drawingName.trim() || undefined,
      ParentProductionOrderNo: isDraft
        ? form.ParentProductionOrderNo
        : (formatSubassemblyNo(form, parent?.DocumentNumber) || form.ParentProductionOrderNo),
      ParentAbsoluteEntry: parent?.AbsoluteEntry ?? form.ParentAbsoluteEntry,
      Warehouse: form.Warehouse || parent?.Warehouse,
      IssWarehouse: form.IssWarehouse || parent?.IssWarehouse,
      Project: form.Project || parent?.Project,
      ProjectName: form.ProjectName || parent?.ProjectName,
      ProductionOrderLines: lines.map((line) => ({
        ...line,
        DrawingNo: line.DrawingNo || form.DrawingNo,
        DrawingName: line.DrawingName || drawingName.trim() || undefined,
        FreeText: (line.FreeText ?? '').trim() || undefined,
        Status: line.Status || status,
      })),
    }
    const message = validateSubassemblyForm(payload, lines)
    if (message) {
      setError(message)
      toast.error(message)
      return
    }
    setSaving(true)
    setError(null)
    try {
      if (isDraft) {
        upsertDraftSubassembly({
          ...payload,
          DraftKey: payload.DraftKey || childId || newDraftSubassemblyKey(loadCreateDraft()?.subassemblies ?? []),
          ProductionOrderLines: payload.ProductionOrderLines,
        })
        toast.success(childId ? 'Sub-assembly updated.' : 'Sub-assembly added.')
        navigate(parentFormPath)
        return
      }
      let childEntry = payload.AbsoluteEntry
      if (childEntry) {
        await updateProductionOrder(childEntry, payload)
      } else {
        await createProductionOrder(payload)
      }
      toast.success(payload.AbsoluteEntry ? 'Sub-assembly updated.' : 'Sub-assembly created.')
      navigate(parentFormPath)
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Failed to save sub-assembly.'
      setError(text)
      toast.error(text)
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  const subassemblyNo = formatSubassemblyNo(form, parent?.DocumentNumber)
  const isExisting = Boolean(form.AbsoluteEntry || (isDraft && childId))

  return (
    <div className="space-y-6">
      <PageHeader
        title={isExisting ? 'Update Sub-assembly' : 'Add Sub-assembly'}
        description={parent?.DocumentNumber != null
          ? `Parent production order ${parent.DocumentNumber}`
          : 'Parent production order'}
      />
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {error}
        </div>
      )}
      <form className="space-y-6" onSubmit={(e) => void handleSubmit(e)}>
        <Card>
          <CardContent className="space-y-6 pt-6">
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
              <Input
                label="Subassembly No."
                value={subassemblyNo}
                disabled
                hint="Parent production order / sequence."
              />
              <Input
                label="Drawing No."
                value={form.DrawingNo ?? ''}
                onChange={(e) => {
                  const DrawingNo = e.target.value
                  setForm({ ...form, DrawingNo })
                  setLines((current) => current.map((line) => ({ ...line, DrawingNo })))
                }}
              />
              <Input
                label="Drawing Name"
                value={drawingName}
                onChange={(e) => {
                  const nextName = e.target.value
                  setDrawingName(nextName)
                  setLines((current) => current.map((line) => ({ ...line, DrawingName: nextName })))
                }}
              />
              <Input
                label="Weight"
                type="number"
                nonNegative
                value={String(form.Weight ?? '')}
                onChange={(e) => setForm({
                  ...form,
                  Weight: e.target.value === '' ? undefined : Number(e.target.value),
                })}
              />
              <Select
                label="Status"
                value={form.Status ?? 'boposPlanned'}
                options={SUBASSEMBLY_STATUS_OPTIONS}
                onChange={(value) => {
                  setForm({ ...form, Status: value })
                  setDraft((current) => ({ ...current, Status: value }))
                  setLines((current) => current.map((line) => ({ ...line, Status: value })))
                }}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="space-y-6 pt-6">
            <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
              <SearchableSelect
                label="Item Code"
                lookupKind="item"
                usePortal={false}
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
                label="Free Text"
                value={draft.FreeText ?? ''}
                onChange={(e) => setDraft({ ...draft, FreeText: e.target.value })}
              />
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
                { key: 'IssuedQuantity', header: 'Issued Qty', accessor: (row) => row.IssuedQuantity ?? 0 },
                { key: 'DrawingNo', header: 'Drawing No.', accessor: (row) => row.DrawingNo || form.DrawingNo || '—' },
                { key: 'DrawingName', header: 'Drawing Name', accessor: (row) => row.DrawingName || drawingName || '—' },
                { key: 'FreeText', header: 'Free Text', accessor: (row) => row.FreeText?.trim() || '—' },
                {
                  key: 'Status',
                  header: 'Status',
                  accessor: (row) => subassemblyStatusLabel(row.Status || form.Status),
                },
                {
                  key: 'Uom',
                  header: 'Stock UoM',
                  accessor: (row) => itemMap[row.ItemNo ?? '']?.stockUom || '—',
                },
              ]}
              actions={(row) => (
                <RowActions>
                  <RowActionButton
                    title={(row.IssuedQuantity ?? 0) > 0 ? 'Issued items cannot be deleted' : 'Delete item'}
                    variant="danger"
                    disabled={(row.IssuedQuantity ?? 0) > 0}
                    icon={<Trash2 className={rowActionIconClassName} />}
                    onClick={() => setLines(lines.filter((line) => line !== row))}
                  />
                </RowActions>
              )}
            />

            <Textarea
              label="Remarks"
              value={form.Remarks ?? ''}
              onChange={(e) => setForm({ ...form, Remarks: e.target.value })}
              placeholder="Remarks for this sub-assembly"
            />

            <div className="flex gap-3">
              <Button type="submit" isLoading={saving}>{isExisting ? 'Update' : 'Add'}</Button>
              <Button type="button" variant="outline" onClick={() => navigate(parentFormPath)}>
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  )
}
