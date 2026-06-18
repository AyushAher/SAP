import { useCallback, useMemo, useState, type FormEvent } from 'react'
import { SelectableSapDataGrid } from '@/Components/shared/SelectableSapDataGrid'
import type { SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Input, SearchableSelect } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { searchItems, searchTaxCodes, searchWarehouses } from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { DocumentLineItem } from '@/types/production'

interface DocumentLinesEditorProps {
  lines: DocumentLineItem[]
  onChange: (lines: DocumentLineItem[]) => void
  showTaxColumns?: boolean
  showFromWarehouse?: boolean
  title?: string
}

const emptyLine = (): DocumentLineItem => ({
  ItemCode: '',
  Quantity: 0,
  UnitPrice: 0,
  TaxCode: '',
  WarehouseCode: '',
  FromWarehouseCode: '',
})

export function DocumentLinesEditor({
  lines,
  onChange,
  showTaxColumns = false,
  showFromWarehouse = false,
  title = 'Document Lines',
}: DocumentLinesEditorProps) {
  const [draft, setDraft] = useState<DocumentLineItem>(emptyLine())
  const [itemLabel, setItemLabel] = useState('')
  const [fromWarehouseLabel, setFromWarehouseLabel] = useState('')
  const [toWarehouseLabel, setToWarehouseLabel] = useState('')
  const [taxLabel, setTaxLabel] = useState('')
  const [taxRates, setTaxRates] = useState<Record<string, number>>({})

  const itemCodes = useMemo(() => lines.map((line) => line.ItemCode), [lines])
  const itemMap = useItemMasterMap(itemCodes)

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

  const searchTaxOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchTaxCodes(search)
    const rates: Record<string, number> = { ...taxRates }
    const options = (response.data ?? []).map((tax) => {
      if (tax.Code) rates[tax.Code] = tax.Rate ?? 0
      return {
        value: tax.Code ?? '',
        label: `${tax.Code ?? ''}${tax.Name ? ` - ${tax.Name}` : ''}`.trim(),
      }
    }).filter((o) => o.value)
    setTaxRates(rates)
    return options
  }, [taxRates])

  const getTaxAmount = (taxCode?: string, lineTotal = 0) => {
    const rate = taxCode ? taxRates[taxCode] ?? 0 : 0
    return (lineTotal * rate) / 100
  }

  const enrichLine = (line: DocumentLineItem): DocumentLineItem => {
    const lineTotal = (line.UnitPrice ?? 0) * (line.Quantity ?? 0)
    const taxTotal = getTaxAmount(line.TaxCode, lineTotal)
    return {
      ...line,
      LineTotal: lineTotal,
      TaxTotal: taxTotal,
      GrossTotal: lineTotal + taxTotal,
    }
  }

  const handleAdd = (e: FormEvent) => {
    e.preventDefault()
    onChange([...lines, enrichLine(draft)])
    setDraft(emptyLine())
    setItemLabel('')
    setFromWarehouseLabel('')
    setToWarehouseLabel('')
    setTaxLabel('')
  }

  const columns: SapColumn<DocumentLineItem>[] = [
    {
      key: 'ItemCode',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemCode, r.ItemDescription ?? itemMap[r.ItemCode ?? '']?.name),
    },
    ...(showFromWarehouse ? [{ key: 'FromWarehouseCode', header: 'From Warehouse', accessor: (r: DocumentLineItem) => r.FromWarehouseCode }] : []),
    { key: 'WarehouseCode', header: showFromWarehouse ? 'To Warehouse' : 'Warehouse', accessor: (r) => r.WarehouseCode },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => r.UnitPrice },
    { key: 'Quantity', header: 'Quantity', accessor: (r) => r.Quantity },
    ...(showTaxColumns ? [
      { key: 'TaxCode', header: 'Tax Code', accessor: (r: DocumentLineItem) => r.TaxCode },
      { key: 'LineTotal', header: 'Line Total', accessor: (r: DocumentLineItem) => r.LineTotal?.toFixed(2) },
      { key: 'TaxTotal', header: 'Tax Total', accessor: (r: DocumentLineItem) => r.TaxTotal?.toFixed(2) },
      { key: 'GrossTotal', header: 'Gross Total', accessor: (r: DocumentLineItem) => r.GrossTotal?.toFixed(2) },
    ] : []),
  ]

  return (
    <div className="space-y-4">
      <form onSubmit={handleAdd} className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <SearchableSelect
          label="Item Code"
          lookupKind="item"
          value={draft.ItemCode ?? ''}
          selectedLabel={itemLabel}
          placeholder="Search item..."
          onSearch={searchItemOptions}
          onChange={(code, option) => {
            const label = option?.label ?? code
            const description = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
            setItemLabel(label)
            setDraft({ ...draft, ItemCode: code, ItemDescription: description })
          }}
        />
        <Input label="Quantity" type="number" value={String(draft.Quantity ?? 0)} onChange={(e) => setDraft({ ...draft, Quantity: Number(e.target.value) })} required />
        <Input label="Unit Price" type="number" value={String(draft.UnitPrice ?? 0)} onChange={(e) => setDraft({ ...draft, UnitPrice: Number(e.target.value) })} required />
        {showFromWarehouse && (
          <SearchableSelect
            label="From Warehouse"
            value={draft.FromWarehouseCode ?? ''}
            selectedLabel={fromWarehouseLabel}
            placeholder="Search warehouse..."
            onSearch={searchWarehouseOptions}
            onChange={(code, option) => {
              setFromWarehouseLabel(option?.label ?? code)
              setDraft({ ...draft, FromWarehouseCode: code })
            }}
          />
        )}
        <SearchableSelect
          label={showFromWarehouse ? 'To Warehouse' : 'Warehouse'}
          value={draft.WarehouseCode ?? ''}
          selectedLabel={toWarehouseLabel}
          placeholder="Search warehouse..."
          onSearch={searchWarehouseOptions}
          onChange={(code, option) => {
            setToWarehouseLabel(option?.label ?? code)
            setDraft({ ...draft, WarehouseCode: code })
          }}
        />
        {showTaxColumns && (
          <SearchableSelect
            label="Tax Code"
            value={draft.TaxCode ?? ''}
            selectedLabel={taxLabel}
            placeholder="Search tax code..."
            onSearch={searchTaxOptions}
            onChange={(code, option) => {
              setTaxLabel(option?.label ?? code)
              setDraft({ ...draft, TaxCode: code })
            }}
          />
        )}
        <div className="md:col-span-2 xl:col-span-4">
          <Button type="submit">Add Item</Button>
        </div>
      </form>

      <SelectableSapDataGrid
        toolbarTitle={title}
        columns={columns}
        data={lines}
        getRowKey={(row) => `${row.ItemCode}-${row.WarehouseCode}-${lines.indexOf(row)}`}
        onRemoveSelected={(selected) => onChange(lines.filter((line) => !selected.includes(line)))}
      />
    </div>
  )
}
