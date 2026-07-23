import { useCallback, useMemo, useRef, useState, type FormEvent } from 'react'
import { SelectableSapDataGrid } from '@/Components/shared/SelectableSapDataGrid'
import type { SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Input, SearchableSelect } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { calculateLineTotals } from '@/helpers/purchaseOrderForm'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { ITEM_DETAIL_FIELDS, searchItems, searchTaxCodes, searchWarehouses } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { PurchaseOrderLineItem } from '@/types/purchaseOrder'

interface PurchaseOrderLinesEditorProps {
  lines: PurchaseOrderLineItem[]
  onChange: (lines: PurchaseOrderLineItem[]) => void
  defaultWarehouse?: string
  title?: string
  readOnly?: boolean
}

const emptyLine = (): PurchaseOrderLineItem => ({
  ItemCode: '',
  Quantity: 0,
  UnitPrice: 0,
  TaxCode: '',
  WarehouseCode: '',
})

export function PurchaseOrderLinesEditor({
  lines,
  onChange,
  defaultWarehouse = '',
  title = 'Items',
  readOnly = false,
}: PurchaseOrderLinesEditorProps) {
  const [draft, setDraft] = useState<PurchaseOrderLineItem>(() => ({
    ...emptyLine(),
    WarehouseCode: defaultWarehouse,
  }))
  const [itemLabel, setItemLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [taxLabel, setTaxLabel] = useState('')
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

  const enrichLine = (line: PurchaseOrderLineItem): PurchaseOrderLineItem => {
    const item = itemMap[line.ItemCode ?? '']
    const uom = line.UomName ?? item?.uom ?? ''
    const perUnitWeight = line.WeightKg != null && line.Quantity
      ? line.WeightKg / (line.Quantity || 1)
      : undefined
    const rate = line.TaxCode ? taxRatesRef.current[line.TaxCode] ?? 0 : 0
    const enriched = calculateLineTotals(
      {
        ...line,
        UomName: uom,
        ItemDescription: line.ItemDescription ?? item?.name,
      },
      rate,
    )
    if (perUnitWeight != null) {
      enriched.WeightKg = perUnitWeight * (enriched.Quantity ?? 0)
    }
    return enriched
  }

  const handleAdd = (e: FormEvent) => {
    e.preventDefault()
    if (readOnly) return
    onChange([...lines, enrichLine({ ...draft, WarehouseCode: draft.WarehouseCode || defaultWarehouse })])
    setDraft({ ...emptyLine(), WarehouseCode: defaultWarehouse })
    setItemLabel('')
    setWarehouseLabel('')
    setTaxLabel('')
  }

  const columns: SapColumn<PurchaseOrderLineItem>[] = [
    {
      key: 'ItemCode',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemCode, r.ItemDescription ?? itemMap[r.ItemCode ?? '']?.name),
    },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'Quantity', header: 'Purchase Qty', accessor: (r) => r.Quantity },
    { key: 'UomName', header: 'Uom Name', accessor: (r) => r.UomName ?? itemMap[r.ItemCode ?? '']?.uom ?? '—' },
    { key: 'StockQty', header: 'Stock Qty', accessor: (r) => r.StockQty ?? '—' },
    { key: 'Uom', header: 'Uom', accessor: (r) => r.UomName ?? itemMap[r.ItemCode ?? '']?.uom ?? '—' },
    { key: 'WeightKg', header: 'Weight (Kg)', accessor: (r) => formatPoCell(r.WeightKg) },
    { key: 'TaxableAmount', header: 'Taxable Amount', accessor: (r) => formatPoCell(r.TaxableAmount ?? r.LineTotal) },
  ]

  return (
    <div className="space-y-4">
      {!readOnly && (
        <form onSubmit={handleAdd} className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
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
              const meta = option?.meta as { InventoryUom?: string; InventoryWeight?: number } | undefined
              setItemLabel(label)
              setDraft({
                ...draft,
                ItemCode: code,
                ItemDescription: description,
                UomName: meta?.InventoryUom ?? '',
                WeightKg: meta?.InventoryWeight ?? 0,
              })
            }}
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
            label="Purchase Qty"
            type="number"
            min="0"
            nonNegative
            value={String(draft.Quantity ?? 0)}
            onChange={(e) => setDraft({ ...draft, Quantity: Number(e.target.value) })}
            required
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
          <div className="md:col-span-2 xl:col-span-4">
            <Button type="submit">Add Item</Button>
          </div>
        </form>
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
