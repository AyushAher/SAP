import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { DocumentLinesEditor } from '@/Components/forms/DocumentLinesEditor'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PreviousNextButtons } from '@/Components/shared/PreviousNextButtons'
import { Button, Card, CardContent, Input, SearchableSelect, Select, Textarea } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName, resolveMasterSelectLabels } from '@/helpers/masterLookup'
import { createInventoryTransfer, getInventoryTransfer, updateInventoryTransfer } from '@/Requests/inventoryTransfers'
import { searchVendors, searchWarehouses } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { DocumentLineItem } from '@/types/production'

const DUTY_OPTIONS = [
  { value: 'tYES', label: 'With Payment Of Duty' },
  { value: 'tNO', label: 'Without Payment Of Duty' },
]

export function StockTransferFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [form, setForm] = useState<Record<string, unknown>>({
    CardCode: '',
    CardName: '',
    FromWarehouse: '',
    ToWarehouse: '',
    DutyStatus: 'tYES',
    DocDate: new Date().toISOString().slice(0, 10),
    DueDate: new Date().toISOString().slice(0, 10),
    JournalMemo: '',
    SalesPersonCode: undefined,
    Comments: '',
    StockTransferLines: [],
  })
  const [lines, setLines] = useState<DocumentLineItem[]>([])
  const [fromWarehouseLabel, setFromWarehouseLabel] = useState('')
  const [toWarehouseLabel, setToWarehouseLabel] = useState('')
  const [vendorLabel, setVendorLabel] = useState('')
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: `${wh.WarehouseCode ?? ''}${wh.City ? ` - ${wh.City}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchVendorOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchVendors(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  useEffect(() => {
    if (!id) return
    getInventoryTransfer(id)
      .then(async (data) => {
        setForm(data)
        setLines((data.StockTransferLines as DocumentLineItem[] | undefined) ?? [])
        if (data.FromWarehouse) setFromWarehouseLabel(String(data.FromWarehouse))
        if (data.ToWarehouse) setToWarehouseLabel(String(data.ToWarehouse))
        if (data.CardCode) {
          const labels = await resolveMasterSelectLabels({ vendorCode: String(data.CardCode) })
          setVendorLabel(labels.vendorLabel ?? formatCodeWithName(String(data.CardCode), String(data.CardName ?? '')))
        }
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!lines.length) {
      setError('Add at least one transfer line.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload = { ...form, StockTransferLines: lines }
      if (id) await updateInventoryTransfer(id, payload)
      else await createInventoryTransfer(payload)
      navigate(ROUTES.INVENTORY_TRANSFERS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  return (
    <div className="space-y-6">
      <PageHeader title={id ? 'Edit Stock Transfer' : 'New Stock Transfer'} />
      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
      <Card>
        <CardContent className="space-y-6 pt-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2">
              <SearchableSelect
                label="Business Partner"
                lookupKind="businessPartner"
                disabled={!!id}
                value={String(form.CardCode ?? '')}
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
              <Select
                label="Duty Status"
                value={String(form.DutyStatus ?? 'tYES')}
                onChange={(value) => setForm({ ...form, DutyStatus: value })}
                options={DUTY_OPTIONS}
              />
              <SearchableSelect
                label="From Warehouse"
                required
                value={String(form.FromWarehouse ?? '')}
                selectedLabel={fromWarehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                onChange={(code, option) => {
                  setFromWarehouseLabel(option?.label ?? code)
                  setForm({ ...form, FromWarehouse: code })
                }}
              />
              <SearchableSelect
                label="To Warehouse"
                required
                value={String(form.ToWarehouse ?? '')}
                selectedLabel={toWarehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                onChange={(code, option) => {
                  setToWarehouseLabel(option?.label ?? code)
                  setForm({ ...form, ToWarehouse: code })
                }}
              />
              <Input label="Doc Date" type="date" value={String(form.DocDate ?? '').slice(0, 10)} onChange={(e) => setForm({ ...form, DocDate: e.target.value })} />
              <Input label="Due Date" type="date" value={String(form.DueDate ?? '').slice(0, 10)} onChange={(e) => setForm({ ...form, DueDate: e.target.value })} />
              <Input label="Sales Person Code" type="number" value={String(form.SalesPersonCode ?? '')} onChange={(e) => setForm({ ...form, SalesPersonCode: Number(e.target.value) })} />
              <Input label="Journal Memo" value={String(form.JournalMemo ?? '')} onChange={(e) => setForm({ ...form, JournalMemo: e.target.value })} />
              <div className="md:col-span-2">
                <Textarea label="Comments" value={String(form.Comments ?? '')} onChange={(e) => setForm({ ...form, Comments: e.target.value })} />
              </div>
            </div>

            <DocumentLinesEditor
              title="Transfer Items"
              lines={lines}
              onChange={setLines}
              showFromWarehouse
            />

            <div className="flex flex-wrap items-center gap-3">
              <Button type="submit" isLoading={saving}>{id ? 'Update Request' : 'Create Request'}</Button>
              <Button type="button" variant="outline" onClick={() => navigate(ROUTES.INVENTORY_TRANSFERS)}>Cancel</Button>
              <PreviousNextButtons
                id={id}
                onPrevious={id && Number(id) > 1 ? () => navigate(`/inventory-transfers/form/${Number(id) - 1}`) : undefined}
                onNext={id ? () => navigate(`/inventory-transfers/form/${Number(id) + 1}`) : undefined}
              />
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
