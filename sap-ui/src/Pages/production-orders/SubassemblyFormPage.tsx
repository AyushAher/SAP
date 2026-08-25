import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, Card, CardContent, Input, SearchableSelect } from '@/Components/ui'
import {
  productionOrderFormPath,
  productionOrderSubassemblyItemsPath,
} from '@/config/constants'
import {
  buildSubassemblyDraftFromParent,
  validateSubassemblyHeaderForm,
} from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import {
  createProductionOrder,
  getProductionOrder,
  updateProductionOrder,
} from '@/Requests/productionOrders'
import { searchItems } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { ProductionOrder } from '@/types/production'

export function SubassemblyFormPage() {
  const { id, childId } = useParams()
  const navigate = useNavigate()
  const [parent, setParent] = useState<ProductionOrder | null>(null)
  const [form, setForm] = useState<ProductionOrder>({})
  const [itemLabel, setItemLabel] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let cancelled = false
    setLoading(true)
    void (async () => {
      try {
        const parentOrder = await getProductionOrder(id)
        if (cancelled) return
        setParent(parentOrder)
        if (childId) {
          const child = await getProductionOrder(childId)
          if (cancelled) return
          setForm(child)
          setItemLabel(child.ItemNumber
            ? `${child.ItemNumber}${child.ProductDescription ? ` - ${child.ProductDescription}` : ''}`
            : '')
        } else {
          setForm(buildSubassemblyDraftFromParent(parentOrder))
          setItemLabel('')
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
  }, [id, childId])

  const searchItemOptions = async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(search)
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const message = validateSubassemblyHeaderForm(form)
    if (message) {
      setError(message)
      toast.error(message)
      return
    }
    setSaving(true)
    setError(null)
    try {
      if (form.AbsoluteEntry) {
        await updateProductionOrder(form.AbsoluteEntry, form)
        toast.success('Sub-assembly updated.')
        navigate(productionOrderSubassemblyItemsPath(id!, form.AbsoluteEntry))
      } else {
        const created = await createProductionOrder(form)
        const childEntry = created.AbsoluteEntry
        toast.success('Sub-assembly created.')
        if (childEntry) {
          navigate(productionOrderSubassemblyItemsPath(id!, childEntry))
        } else {
          navigate(productionOrderFormPath(id!))
        }
      }
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Failed to save sub-assembly.'
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
        title={form.AbsoluteEntry ? 'Update Sub-assembly' : 'Add Sub-assembly'}
        description={parent?.DocumentNumber != null
          ? `Parent production order ${parent.DocumentNumber}`
          : 'Parent production order'}
      />
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {error}
        </div>
      )}
      <Card>
        <CardContent className="pt-6">
          <form className="space-y-6" onSubmit={(e) => void handleSubmit(e)}>
            <div className="grid gap-4 md:grid-cols-3">
              <Input
                label="Subassembly No."
                value={form.DocumentNumber != null ? String(form.DocumentNumber) : ''}
                disabled
                hint="Assigned by SAP after save."
              />
              <Input
                label="Drawing No."
                value={form.DrawingNo ?? ''}
                onChange={(e) => setForm({ ...form, DrawingNo: e.target.value })}
              />
              <Input
                label="Drawing Name"
                value={form.ProductDescription ?? ''}
                onChange={(e) => setForm({ ...form, ProductDescription: e.target.value })}
              />
              <SearchableSelect
                label="Product No."
                required
                lookupKind="item"
                value={form.ItemNumber ?? ''}
                selectedLabel={itemLabel}
                placeholder="Search item..."
                onSearch={searchItemOptions}
                onChange={(code, option) => {
                  const label = option?.label ?? code
                  const name = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
                  setItemLabel(label)
                  setForm({
                    ...form,
                    ItemNumber: code,
                    ProductDescription: form.ProductDescription || name,
                  })
                }}
              />
              <Input
                label="Planned Qty"
                type="number"
                nonNegative
                required
                value={String(form.PlannedQuantity ?? 0)}
                onChange={(e) => setForm({ ...form, PlannedQuantity: Number(e.target.value) })}
              />
            </div>
            <div className="flex gap-3">
              <Button type="submit" isLoading={saving}>{form.AbsoluteEntry ? 'Update' : 'Add'}</Button>
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
