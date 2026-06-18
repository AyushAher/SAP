import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SelectableSapDataGrid } from '@/Components/shared/SelectableSapDataGrid'
import type { SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Card, CardContent, Input, SearchableSelect, Select } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName, resolveMasterSelectLabels } from '@/helpers/masterLookup'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { createProductionOrder, getProductionOrder, updateProductionOrder } from '@/Requests/productionOrders'
import { listSalesOrders, searchCustomers, searchItems, searchProjects, searchWarehouses } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

const STATUS_OPTIONS = [
  { value: 'boposPlanned', label: 'Planned' },
  { value: 'boposReleased', label: 'Released' },
  { value: 'boposClosed', label: 'Closed' },
  { value: 'boposCancelled', label: 'Cancelled' },
]

const TYPE_OPTIONS = [
  { value: 'bopotStandard', label: 'Standard' },
  { value: 'bopotSpecial', label: 'Special' },
]

const CATEGORY_OPTIONS = [
  { value: 'JOB', label: 'JOB' },
  { value: 'EXT', label: 'EXT' },
  { value: 'INT', label: 'INT' },
]

export function ProductionOrderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [form, setForm] = useState<ProductionOrder>({
    ItemNumber: '',
    PlannedQuantity: 0,
    Warehouse: '',
    Project: '',
    Status: 'boposPlanned',
    Type: 'bopotStandard',
    ProductionCategory: 'JOB',
    ProductionOrderLines: [],
  })
  const [lines, setLines] = useState<ProductionOrderLine[]>([])
  const [draftLine, setDraftLine] = useState<ProductionOrderLine>({ ItemNo: '', PlannedQuantity: 0 })
  const [draftItemLabel, setDraftItemLabel] = useState('')
  const [customerLabel, setCustomerLabel] = useState('')
  const [itemLabel, setItemLabel] = useState('')
  const [projectLabel, setProjectLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [salesOrderLabel, setSalesOrderLabel] = useState('')
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const searchCustomerOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchCustomers(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchItemOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(search)
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: `${wh.WarehouseCode ?? ''}${wh.City ? ` - ${wh.City}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchProjectOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchProjects(search)
    return (response.data ?? []).map((p) => ({
      value: p.Code ?? '',
      label: `${p.Code ?? ''} - ${p.Name ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchSalesOrderOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await listSalesOrders(search, form.CustomerCode)
    return (response.data ?? []).map((so) => ({
      value: String(so.DocNum ?? so.DocEntry ?? ''),
      label: `${so.DocNum ?? so.DocEntry ?? ''}${so.NumAtCard ? ` - ${so.NumAtCard}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [form.CustomerCode])

  useEffect(() => {
    if (!id) return
    getProductionOrder(id)
      .then(async (po) => {
        setForm(po)
        setLines(po.ProductionOrderLines ?? [])
        const labels = await resolveMasterSelectLabels({
          customerCode: po.CustomerCode,
          itemCode: po.ItemNumber,
          projectCode: po.Project,
        })
        if (po.CustomerCode) {
          setCustomerLabel(labels.customerLabel ?? formatCodeWithName(po.CustomerCode, po.CustomerName))
        }
        if (po.ItemNumber) {
          setItemLabel(labels.itemLabel ?? formatCodeWithName(po.ItemNumber, po.ProductDescription))
        }
        if (po.Project) {
          setProjectLabel(labels.projectLabel ?? formatCodeWithName(po.Project, po.ProjectName))
        }
        if (po.Warehouse) setWarehouseLabel(String(po.Warehouse))
        if (po.SalesOrderDocNum) setSalesOrderLabel(String(po.SalesOrderDocNum))
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  const lineItemCodes = useMemo(() => lines.map((line) => line.ItemNo), [lines])
  const lineItemMap = useItemMasterMap(lineItemCodes)

  const applyCategoryDefaults = (category: string, nextForm: ProductionOrder, nextLines: ProductionOrderLine[]) => {
    const updated = { ...nextForm, ProductionCategory: category }
    const updatedLines = nextLines.map((line) => ({ ...line }))
    if (category === 'JOB') {
      updated.IssWarehouse = 'Store1'
      updated.Warehouse = 'Subcon'
      updatedLines.forEach((line) => { line.Warehouse = 'Store1' })
    } else if (category === 'EXT') {
      updated.IssWarehouse = 'PBPL(S)'
      updated.Warehouse = 'PBPL(S)'
      updatedLines.forEach((line) => { line.Warehouse = 'PBPL(S)' })
    } else if (category === 'INT') {
      updated.Warehouse = 'WIP'
      updatedLines.forEach((line) => { line.Warehouse = 'Store1' })
    }
    return { updated, updatedLines }
  }

  const handleAddLine = () => {
    const nextLine: ProductionOrderLine = {
      ...draftLine,
      LineNumber: (lines.at(-1)?.LineNumber ?? 0) + 1,
      DocumentAbsoluteEntry: form.AbsoluteEntry,
    }
    setLines((prev) => [...prev, nextLine])
    setDraftLine({ ItemNo: '', PlannedQuantity: 0 })
    setDraftItemLabel('')
  }

  const lineColumns: SapColumn<ProductionOrderLine>[] = [
    {
      key: 'ItemNo',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemNo, r.ItemName ?? lineItemMap[r.ItemNo ?? '']?.name),
    },
    { key: 'PlannedQuantity', header: 'Planned Qty', accessor: (r) => r.PlannedQuantity },
    { key: 'Warehouse', header: 'Warehouse', accessor: (r) => r.Warehouse },
  ]

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!form.ItemNumber) {
      setError('Product number is required.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const { updated, updatedLines } = applyCategoryDefaults(form.ProductionCategory ?? 'JOB', form, lines)
      const payload = {
        ...updated,
        ProductionOrderLines: updatedLines,
        PostingDate: updated.PostingDate ?? new Date().toISOString(),
      }
      if (id) await updateProductionOrder(Number(id), payload)
      else await createProductionOrder(payload)
      navigate(ROUTES.PRODUCTION_ORDERS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  return (
    <div className="space-y-6">
      <PageHeader title={id ? `Update Production Order #${form.DocumentNumber ?? id}` : 'New Production Order'} />
      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
      <Card>
        <CardContent className="space-y-6 pt-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2">
              <SearchableSelect
                label="Customer"
                lookupKind="businessPartner"
                value={form.CustomerCode ?? ''}
                selectedLabel={customerLabel}
                placeholder="Search customer..."
                onSearch={searchCustomerOptions}
                onChange={(code, option) => {
                  setCustomerLabel(option?.label ?? code)
                  setForm({ ...form, CustomerCode: code })
                }}
              />
              <SearchableSelect
                label="Sales Order"
                value={String(form.SalesOrderDocNum ?? '')}
                selectedLabel={salesOrderLabel}
                placeholder="Search sales order..."
                onSearch={searchSalesOrderOptions}
                onChange={(value) => {
                  setSalesOrderLabel(value)
                  setForm({ ...form, SalesOrderDocNum: Number(value) })
                }}
              />
              <SearchableSelect
                label="Project"
                lookupKind="project"
                value={form.Project ?? ''}
                selectedLabel={projectLabel}
                placeholder="Search project..."
                onSearch={searchProjectOptions}
                onChange={(code, option) => {
                  setProjectLabel(option?.label ?? code)
                  setForm({ ...form, Project: code })
                }}
              />
              <SearchableSelect
                label="Product No."
                lookupKind="item"
                disabled={!!form.AbsoluteEntry}
                value={form.ItemNumber ?? ''}
                selectedLabel={itemLabel}
                placeholder="Search item..."
                onSearch={searchItemOptions}
                onChange={(code, option) => {
                  setItemLabel(option?.label ?? code)
                  setForm({ ...form, ItemNumber: code })
                }}
              />
              <Select label="Status" value={form.Status ?? 'boposPlanned'} onChange={(value) => setForm({ ...form, Status: value })} options={STATUS_OPTIONS} />
              <Select label="Type" value={form.Type ?? 'bopotStandard'} onChange={(value) => setForm({ ...form, Type: value })} options={TYPE_OPTIONS} disabled={!!form.AbsoluteEntry} />
              <Select
                label="Production Category"
                value={form.ProductionCategory ?? 'JOB'}
                onChange={(value) => {
                  const { updated, updatedLines } = applyCategoryDefaults(value, form, lines)
                  setForm(updated)
                  setLines(updatedLines)
                }}
                options={CATEGORY_OPTIONS}
              />
              <Input label="Planned Qty" type="number" value={String(form.PlannedQuantity ?? 0)} onChange={(e) => setForm({ ...form, PlannedQuantity: Number(e.target.value) })} />
              <SearchableSelect
                label="Receipt Warehouse"
                value={form.Warehouse ?? ''}
                selectedLabel={warehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                onChange={(code, option) => {
                  setWarehouseLabel(option?.label ?? code)
                  setForm({ ...form, Warehouse: code })
                }}
              />
              <Input label="Drawing No." value={form.DrawingNo ?? ''} onChange={(e) => setForm({ ...form, DrawingNo: e.target.value })} />
              <Input label="Remarks" value={form.Remarks ?? ''} onChange={(e) => setForm({ ...form, Remarks: e.target.value })} />
            </div>

            <div className="grid gap-4 md:grid-cols-4">
              <SearchableSelect
                label="Line Item"
                lookupKind="item"
                value={draftLine.ItemNo ?? ''}
                selectedLabel={draftItemLabel}
                placeholder="Search item..."
                onSearch={searchItemOptions}
                onChange={(code, option) => {
                  const label = option?.label ?? code
                  setDraftItemLabel(label)
                  setDraftLine({
                    ...draftLine,
                    ItemNo: code,
                    ItemName: label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : undefined,
                  })
                }}
              />
              <Input label="Line Planned Qty" type="number" value={String(draftLine.PlannedQuantity ?? 0)} onChange={(e) => setDraftLine({ ...draftLine, PlannedQuantity: Number(e.target.value) })} />
              <div className="flex items-end">
                <Button type="button" variant="outline" onClick={handleAddLine}>Add Line</Button>
              </div>
            </div>

            <SelectableSapDataGrid
              toolbarTitle="Production Order Lines"
              columns={lineColumns}
              data={lines}
              getRowKey={(row) => `${row.LineNumber}-${row.ItemNo}`}
              onRemoveSelected={(selected) => setLines(lines.filter((line) => !selected.includes(line)))}
            />

            <div className="flex gap-3">
              <Button type="submit" isLoading={saving}>{form.AbsoluteEntry ? 'Update' : 'Add'}</Button>
              <Button type="button" variant="outline" onClick={() => navigate(ROUTES.PRODUCTION_ORDERS)}>Cancel</Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
