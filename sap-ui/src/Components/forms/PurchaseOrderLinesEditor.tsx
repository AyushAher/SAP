import { useCallback, useMemo, useRef, useState } from 'react'
import { SelectableSapDataGrid } from '@/Components/shared/SelectableSapDataGrid'
import type { SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Input, SearchableSelect } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { calculateLineTotals } from '@/helpers/purchaseOrderForm'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import {
  ITEM_DETAIL_FIELDS,
  searchHsnCodes,
  searchItems,
  searchProjects,
  searchSacCodes,
  searchTaxCodes,
  searchWarehouses,
  type MasterItem,
} from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { PurchaseOrderLineItem } from '@/types/purchaseOrder'

interface PurchaseOrderLinesEditorProps {
  lines: PurchaseOrderLineItem[]
  onChange: (lines: PurchaseOrderLineItem[]) => void
  defaultWarehouse?: string
  defaultProject?: string
  title?: string
  readOnly?: boolean
}

const emptyLine = (): PurchaseOrderLineItem => ({
  ItemCode: '',
  Quantity: 0,
  UnitPrice: 0,
  DiscountPercent: undefined,
  TaxCode: '',
  WarehouseCode: '',
  ProjectCode: '',
  CostingCode: '',
  HSNEntry: undefined,
  SACEntry: undefined,
})

export function PurchaseOrderLinesEditor({
  lines,
  onChange,
  defaultWarehouse = '',
  defaultProject = '',
  title = 'Items',
  readOnly = false,
}: PurchaseOrderLinesEditorProps) {
  const [draft, setDraft] = useState<PurchaseOrderLineItem>(() => ({
    ...emptyLine(),
    WarehouseCode: defaultWarehouse,
    ProjectCode: defaultProject,
  }))
  const [itemLabel, setItemLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [taxLabel, setTaxLabel] = useState('')
  const [hsnLabel, setHsnLabel] = useState('')
  const [sacLabel, setSacLabel] = useState('')
  const [projectLabel, setProjectLabel] = useState('')
  const taxRatesRef = useRef<Record<string, number>>({})

  const itemCodes = useMemo(() => lines.map((line) => line.ItemCode), [lines])
  const itemMap = useItemMasterMap(itemCodes)

  const searchItemOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(search, 20, ITEM_DETAIL_FIELDS)
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
      meta: item,
    })).filter((o) => o.value)
  }, [])

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: `${wh.WarehouseCode ?? ''}${wh.City ? ` - ${wh.City}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchTaxOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchTaxCodes(search)
    return (response.data ?? []).map((tax) => {
      if (tax.Code) taxRatesRef.current[tax.Code] = tax.Rate ?? 0
      return {
        value: tax.Code ?? '',
        label: `${tax.Code ?? ''}${tax.Name ? ` - ${tax.Name}` : ''}`.trim(),
        meta: { rate: tax.Rate ?? 0 },
      }
    }).filter((o) => o.value)
  }, [])

  const searchHsnOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchHsnCodes(search)
    return (response.data ?? []).map((hsn) => ({
      value: String(hsn.AbsEntry ?? ''),
      label: hsn.DisplayLabel ?? String(hsn.AbsEntry ?? ''),
      meta: hsn,
    })).filter((o) => o.value)
  }, [])

  const searchSacOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchSacCodes(search)
    return (response.data ?? []).map((sac) => ({
      value: String(sac.AbsEntry ?? ''),
      label: sac.DisplayLabel ?? String(sac.AbsEntry ?? ''),
      meta: sac,
    })).filter((o) => o.value)
  }, [])

  const searchProjectOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchProjects(search)
    return (response.data ?? []).map((p) => ({
      value: p.Code ?? '',
      label: `${p.Code ?? ''} - ${p.Name ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const enrichLine = (line: PurchaseOrderLineItem): PurchaseOrderLineItem => {
    const item = itemMap[line.ItemCode ?? '']
    const uom = line.UoMCode ?? line.UomName ?? item?.uom ?? ''
    const rate = line.TaxCode ? taxRatesRef.current[line.TaxCode] ?? 0 : 0
    return calculateLineTotals(
      {
        ...line,
        UoMCode: uom || undefined,
        UomName: uom || undefined,
        ItemDescription: line.ItemDescription ?? item?.name,
      },
      rate,
    )
  }

  const resetDraftLabels = () => {
    setItemLabel('')
    setWarehouseLabel('')
    setTaxLabel('')
    setHsnLabel('')
    setSacLabel('')
    setProjectLabel('')
  }

  const columns: SapColumn<PurchaseOrderLineItem>[] = [
    {
      key: 'ItemCode',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemCode, r.ItemDescription ?? itemMap[r.ItemCode ?? '']?.name),
    },
    { key: 'WarehouseCode', header: 'Whse', accessor: (r) => r.WarehouseCode ?? '—' },
    { key: 'Quantity', header: 'Qty', accessor: (r) => r.Quantity },
    { key: 'UoMCode', header: 'UoM', accessor: (r) => r.UoMCode ?? r.UomName ?? itemMap[r.ItemCode ?? '']?.uom ?? '—' },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'DiscountPercent', header: 'Disc %', accessor: (r) => r.DiscountPercent ?? '—' },
    { key: 'TaxCode', header: 'Tax', accessor: (r) => r.TaxCode ?? '—' },
    { key: 'HSNEntry', header: 'HSN', accessor: (r) => r.HsnLabel ?? (r.HSNEntry != null ? String(r.HSNEntry) : '—') },
    { key: 'SACEntry', header: 'SAC', accessor: (r) => r.SacLabel ?? (r.SACEntry != null ? String(r.SACEntry) : '—') },
    { key: 'ProjectCode', header: 'Project', accessor: (r) => r.ProjectCode ?? '—' },
    { key: 'CostingCode', header: 'Cost Ctr', accessor: (r) => r.CostingCode ?? '—' },
    { key: 'TaxableAmount', header: 'Taxable', accessor: (r) => formatPoCell(r.TaxableAmount ?? r.LineTotal) },
    { key: 'GrossTotal', header: 'Gross', accessor: (r) => formatPoCell(r.GrossTotal) },
  ]

  return (
    <div className="space-y-4">
      {!readOnly && (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <SearchableSelect
            label="Item"
            lookupKind="item"
            value={draft.ItemCode ?? ''}
            selectedLabel={itemLabel}
            placeholder="Search item..."
            onSearch={searchItemOptions}
            onChange={(code, option) => {
              const label = option?.label ?? code
              const description = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
              const meta = option?.meta as MasterItem | undefined
              const uom = meta?.PurchaseUnit || meta?.InventoryUom || ''
              const chapterAbs = meta?.ChapterID != null && meta.ChapterID !== ''
                ? Number(meta.ChapterID)
                : undefined
              setItemLabel(label)
              setDraft({
                ...draft,
                ItemCode: code,
                ItemDescription: description,
                UoMCode: uom || undefined,
                UomName: uom || undefined,
                WeightKg: meta?.InventoryWeight ?? 0,
                TaxCode: draft.TaxCode || meta?.PurchaseVatGroup || '',
                HSNEntry: Number.isFinite(chapterAbs) ? chapterAbs : draft.HSNEntry,
                HsnLabel: Number.isFinite(chapterAbs) ? String(chapterAbs) : draft.HsnLabel,
                WarehouseCode: draft.WarehouseCode || defaultWarehouse,
                ProjectCode: draft.ProjectCode || defaultProject,
              })
              if (meta?.PurchaseVatGroup) setTaxLabel(meta.PurchaseVatGroup)
              if (Number.isFinite(chapterAbs)) setHsnLabel(String(chapterAbs))
            }}
          />
          <Input
            label="Description"
            value={draft.ItemDescription ?? ''}
            onChange={(e) => setDraft({ ...draft, ItemDescription: e.target.value })}
          />
          <SearchableSelect
            label="Warehouse"
            value={draft.WarehouseCode ?? ''}
            selectedLabel={warehouseLabel}
            placeholder="Search warehouse..."
            onSearch={searchWarehouseOptions}
            onChange={(code, option) => {
              setWarehouseLabel(option?.label ?? code)
              setDraft({ ...draft, WarehouseCode: code })
            }}
          />
          <Input
            label="UoM"
            value={draft.UoMCode ?? draft.UomName ?? ''}
            onChange={(e) => setDraft({ ...draft, UoMCode: e.target.value, UomName: e.target.value })}
          />
          <Input
            label="Quantity"
            type="number"
            min="0"
            nonNegative
            value={String(draft.Quantity ?? 0)}
            onChange={(e) => setDraft({ ...draft, Quantity: Number(e.target.value) })}
            required
          />
          <Input
            label="Unit Price"
            type="number"
            step="0.01"
            min="0"
            nonNegative
            value={String(draft.UnitPrice ?? 0)}
            onChange={(e) => setDraft({ ...draft, UnitPrice: Number(e.target.value) })}
            required
          />
          <Input
            label="Discount %"
            type="number"
            min="0"
            nonNegative
            value={draft.DiscountPercent != null ? String(draft.DiscountPercent) : ''}
            onChange={(e) => setDraft({
              ...draft,
              DiscountPercent: e.target.value === '' ? undefined : Number(e.target.value),
            })}
          />
          <SearchableSelect
            label="Tax Code"
            value={draft.TaxCode ?? ''}
            selectedLabel={taxLabel}
            placeholder="Search tax code..."
            onSearch={searchTaxOptions}
            onChange={(code, option) => {
              const meta = option?.meta as { rate?: number } | undefined
              if (code && meta?.rate != null) taxRatesRef.current[code] = meta.rate
              setTaxLabel(option?.label ?? code)
              setDraft({ ...draft, TaxCode: code })
            }}
          />
          <SearchableSelect
            label="HSN"
            value={draft.HSNEntry != null ? String(draft.HSNEntry) : ''}
            selectedLabel={hsnLabel}
            placeholder="Search HSN..."
            onSearch={searchHsnOptions}
            onChange={(value, option) => {
              const abs = value ? Number(value) : undefined
              setHsnLabel(option?.label ?? value)
              setDraft({
                ...draft,
                HSNEntry: Number.isFinite(abs) ? abs : undefined,
                HsnLabel: option?.label ?? value,
              })
            }}
          />
          <SearchableSelect
            label="SAC"
            value={draft.SACEntry != null ? String(draft.SACEntry) : ''}
            selectedLabel={sacLabel}
            placeholder="Search SAC..."
            onSearch={searchSacOptions}
            onChange={(value, option) => {
              const abs = value ? Number(value) : undefined
              setSacLabel(option?.label ?? value)
              setDraft({
                ...draft,
                SACEntry: Number.isFinite(abs) ? abs : undefined,
                SacLabel: option?.label ?? value,
              })
            }}
          />
          <SearchableSelect
            label="Project"
            lookupKind="project"
            value={draft.ProjectCode ?? ''}
            selectedLabel={projectLabel}
            placeholder="Search project..."
            onSearch={searchProjectOptions}
            onChange={(code, option) => {
              setProjectLabel(option?.label ?? code)
              setDraft({ ...draft, ProjectCode: code })
            }}
          />
          <Input
            label="Cost Center"
            value={draft.CostingCode ?? ''}
            onChange={(e) => setDraft({ ...draft, CostingCode: e.target.value })}
          />
          <div className="md:col-span-2 xl:col-span-4">
            <Button
              type="button"
              onClick={() => {
                if (!draft.ItemCode) return
                onChange([
                  ...lines,
                  enrichLine({
                    ...draft,
                    WarehouseCode: draft.WarehouseCode || defaultWarehouse,
                    ProjectCode: draft.ProjectCode || defaultProject || undefined,
                  }),
                ])
                setDraft({
                  ...emptyLine(),
                  WarehouseCode: defaultWarehouse,
                  ProjectCode: defaultProject,
                })
                resetDraftLabels()
              }}
            >
              Add Item
            </Button>
          </div>
        </div>
      )}

      <SelectableSapDataGrid
        toolbarTitle={title}
        columns={columns}
        data={lines.map((line) => enrichLine(line))}
        getRowKey={(row) => `${row.ItemCode}-${row.WarehouseCode}-${lines.indexOf(row)}`}
        onRemoveSelected={readOnly ? undefined : (selected) => onChange(lines.filter((line) => !selected.includes(line)))}
      />
    </div>
  )
}

function formatPoCell(value: number | undefined | null): string {
  if (value == null || Number.isNaN(value)) return '—'
  return Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
