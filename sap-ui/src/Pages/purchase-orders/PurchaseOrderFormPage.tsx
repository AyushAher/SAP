import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { DocumentLinesEditor } from '@/Components/forms/DocumentLinesEditor'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PreviousNextButtons } from '@/Components/shared/PreviousNextButtons'
import { Button, Card, CardContent, Input, SearchableSelect, Textarea } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName, resolveMasterSelectLabels } from '@/helpers/masterLookup'
import { createPurchaseOrder, getPurchaseOrder, updatePurchaseOrder, type PurchaseOrder } from '@/Requests/purchaseOrders'
import { searchProjects, searchVendors } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { DocumentLineItem } from '@/types/production'

export function PurchaseOrderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState<Partial<PurchaseOrder>>({
    CardCode: '',
    CardName: '',
    Project: '',
    Comments: '',
    JournalMemo: '',
    SalesPersonCode: undefined,
    DocDate: new Date().toISOString().slice(0, 10),
    DueDate: new Date().toISOString().slice(0, 10),
    DocumentLines: [],
  })
  const [lines, setLines] = useState<DocumentLineItem[]>([])
  const [vendorLabel, setVendorLabel] = useState('')
  const [projectLabel, setProjectLabel] = useState('')

  const searchVendorOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchVendors(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchProjectOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchProjects(search)
    return (response.data ?? []).map((p) => ({
      value: p.Code ?? '',
      label: `${p.Code ?? ''} - ${p.Name ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  useEffect(() => {
    if (!id) return
    getPurchaseOrder(id)
      .then(async (po) => {
        setForm(po)
        setLines((po.DocumentLines as DocumentLineItem[] | undefined) ?? [])
        const labels = await resolveMasterSelectLabels({
          vendorCode: po.CardCode,
          projectCode: po.Project,
        })
        if (po.CardCode) {
          setVendorLabel(labels.vendorLabel ?? formatCodeWithName(po.CardCode, po.CardName))
        }
        if (po.Project) {
          setProjectLabel(labels.projectLabel ?? formatCodeWithName(po.Project))
        }
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!lines.length) {
      setError('Add at least one line item.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload = {
        ...form,
        DocumentLines: lines,
        PostingDate: form.PostingDate ?? new Date().toISOString(),
        BPLId: form.BPLId ?? 1,
      } as PurchaseOrder
      if (id) await updatePurchaseOrder(Number(id), payload)
      else await createPurchaseOrder(payload)
      navigate(ROUTES.PURCHASE_ORDERS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center text-slate-500">Loading...</div>

  return (
    <div className="space-y-6">
      <PageHeader title={id ? 'Edit Purchase Order' : 'New Purchase Order'} />
      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
      <Card>
        <CardContent className="space-y-6 pt-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2">
              <SearchableSelect
                label="Vendor"
                lookupKind="businessPartner"
                required
                disabled={!!id}
                value={form.CardCode ?? ''}
                selectedLabel={vendorLabel}
                placeholder="Search vendor..."
                onSearch={searchVendorOptions}
                onChange={(cardCode, option) => {
                  const label = option?.label ?? cardCode
                  const cardName = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
                  setVendorLabel(label)
                  setForm({ ...form, CardCode: cardCode, CardName: cardName })
                }}
              />
              <SearchableSelect
                label="Project"
                lookupKind="project"
                value={form.Project ?? ''}
                selectedLabel={projectLabel}
                placeholder="Search project..."
                onSearch={searchProjectOptions}
                onChange={(projectCode, option) => {
                  setProjectLabel(option?.label ?? projectCode)
                  setForm({ ...form, Project: projectCode })
                }}
              />
              <Input label="Doc Date" type="date" value={String(form.DocDate ?? '').slice(0, 10)} onChange={(e) => setForm({ ...form, DocDate: e.target.value })} />
              <Input label="Due Date" type="date" value={String(form.DueDate ?? '').slice(0, 10)} onChange={(e) => setForm({ ...form, DueDate: e.target.value })} />
              <Input label="Journal Memo" value={String(form.JournalMemo ?? '')} onChange={(e) => setForm({ ...form, JournalMemo: e.target.value })} />
              <Input label="Sales Person Code" type="number" value={String(form.SalesPersonCode ?? '')} onChange={(e) => setForm({ ...form, SalesPersonCode: Number(e.target.value) })} />
              <div className="md:col-span-2">
                <Textarea label="Comments" value={String(form.Comments ?? '')} onChange={(e) => setForm({ ...form, Comments: e.target.value })} />
              </div>
            </div>

            <DocumentLinesEditor
              title="Purchase Order Items"
              lines={lines}
              onChange={setLines}
              showTaxColumns
            />

            <div className="flex flex-wrap items-center gap-3">
              <Button type="submit" isLoading={saving}>Submit</Button>
              <Button type="button" variant="outline" onClick={() => navigate(ROUTES.PURCHASE_ORDERS)}>Cancel</Button>
              <PreviousNextButtons
                id={id}
                onPrevious={id && Number(id) > 1 ? () => navigate(`/purchase-orders/form/${Number(id) - 1}`) : undefined}
                onNext={id ? () => navigate(`/purchase-orders/form/${Number(id) + 1}`) : undefined}
              />
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
