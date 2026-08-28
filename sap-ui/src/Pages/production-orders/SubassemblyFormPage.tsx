import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, Card, CardContent, Input } from '@/Components/ui'
import {
  productionOrderFormPath,
  productionOrderSubassemblyItemsPath,
} from '@/config/constants'
import {
  buildSubassemblyDraftFromParent,
  formatSubassemblyNo,
  validateSubassemblyHeaderForm,
} from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import {
  createProductionOrder,
  getProductionOrder,
  listSubassemblies,
  updateProductionOrder,
} from '@/Requests/productionOrders'
import type { ProductionOrder } from '@/types/production'

export function SubassemblyFormPage() {
  const { id, childId } = useParams()
  const navigate = useNavigate()
  const [parent, setParent] = useState<ProductionOrder | null>(null)
  const [form, setForm] = useState<ProductionOrder>({})
  const [drawingName, setDrawingName] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let cancelled = false
    setLoading(true)
    void (async () => {
      try {
        const [parentOrder, siblings] = await Promise.all([
          getProductionOrder(id),
          listSubassemblies(id, { includeCancelled: true }),
        ])
        if (cancelled) return
        setParent(parentOrder)
        if (childId) {
          const child = await getProductionOrder(childId)
          if (cancelled) return
          setForm({
            ...child,
            ItemNumber: parentOrder.ItemNumber ?? child.ItemNumber,
            ProductDescription: parentOrder.ProductDescription ?? child.ProductDescription,
          })
          const childDesc = (child.ProductDescription ?? '').trim()
          const parentDesc = (parentOrder.ProductDescription ?? '').trim()
          setDrawingName(childDesc && childDesc !== parentDesc ? childDesc : '')
        } else {
          setForm(buildSubassemblyDraftFromParent(parentOrder, siblings))
          setDrawingName('')
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

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const payload: ProductionOrder = {
      ...form,
      ItemNumber: parent?.ItemNumber ?? form.ItemNumber,
      ProductDescription: parent?.ProductDescription ?? form.ProductDescription,
    }
    const message = validateSubassemblyHeaderForm(payload)
    if (message) {
      setError(message)
      toast.error(message)
      return
    }
    setSaving(true)
    setError(null)
    try {
      if (payload.AbsoluteEntry) {
        await updateProductionOrder(payload.AbsoluteEntry, payload)
        toast.success('Sub-assembly updated.')
        navigate(productionOrderSubassemblyItemsPath(id!, payload.AbsoluteEntry))
      } else {
        const created = await createProductionOrder(payload)
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

  const productLabel = [parent?.ItemNumber ?? form.ItemNumber, parent?.ProductDescription]
    .filter(Boolean)
    .join(' - ')
  const subassemblyNo = form.AbsoluteEntry
    ? formatSubassemblyNo(form, parent?.DocumentNumber)
    : (form.ParentProductionOrderNo ?? '')

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
                value={subassemblyNo}
                disabled
                hint="Parent production order / sequence."
              />
              <Input
                label="Product No."
                value={productLabel}
                disabled
                hint="Same product as the parent production order."
              />
              <Input
                label="Planned Qty"
                type="number"
                nonNegative
                required
                value={String(form.PlannedQuantity ?? 0)}
                onChange={(e) => setForm({ ...form, PlannedQuantity: Number(e.target.value) })}
              />
              <Input
                label="Drawing No."
                value={form.DrawingNo ?? ''}
                onChange={(e) => setForm({ ...form, DrawingNo: e.target.value })}
              />
              <Input
                label="Drawing Name"
                value={drawingName}
                onChange={(e) => setDrawingName(e.target.value)}
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
