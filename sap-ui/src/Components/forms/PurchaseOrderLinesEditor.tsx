import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import {
  RowActionButton,
  RowActions,
  rowActionIconClassName,
} from '@/Components/shared/RowActions'
import { Button, Input, Modal, SearchableSelect, Textarea } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import {
  applyStockPurchaseQty,
  calculateLineTotals,
  calcItemsPerUnit,
  calcUseBaseUnits,
  resolveLineUoms,
  resolvePurchaseUnit,
  withItemsPerUnit,
  withPurchaseQty,
  withStockQty,
} from '@/helpers/purchaseOrderForm'
import { pickHsnFromChapterId } from '@/helpers/hsnResolve'
import { isServicePoDocType, PO_TN } from '@/helpers/purchaseOrderTnValidation'
import { toast } from '@/helpers/toast'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import {
  listPurchaseUoms,
  lookupItem,
  searchGlAccounts,
  searchHsnCodes,
  searchItems,
  searchProjects,
  searchSacCodes,
  searchTaxCodes,
  searchWarehouses,
  formatWarehouseOptionLabel,
  type MasterItem,
  type MasterPurchaseUom,
  type MasterWarehouse,
} from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { PurchaseOrderLineItem } from '@/types/purchaseOrder'

async function resolveWarehouseLocation(warehouseCode: string): Promise<number | undefined> {
  const response = await searchWarehouses(warehouseCode, 20)
  const match = (response.data ?? []).find((wh) => wh.WarehouseCode === warehouseCode)
  const loc = match?.Location
  return loc != null && Number.isFinite(loc) ? loc : undefined
}

interface PurchaseOrderLinesEditorProps {
  lines: PurchaseOrderLineItem[]
  onChange: (lines: PurchaseOrderLineItem[]) => void
  defaultWarehouse?: string
  defaultProject?: string
  /** SAP DocType: dDocument_Items | dDocument_Service */
  docType?: string
  title?: string
  readOnly?: boolean
}

type LineRow = PurchaseOrderLineItem & { __rowIndex: number }

const emptyLine = (): PurchaseOrderLineItem => ({
  ItemCode: '',
  ItemDescription: '',
  FreeText: '',
  Quantity: 0,
  StockQty: 0,
  StockUom: '',
  UoMCode: '',
  UnitsOfMeasurment: undefined,
  UnitPrice: 0,
  DiscountPercent: undefined,
  TaxCode: '',
  WarehouseCode: '',
  ProjectCode: '',
  AccountCode: '',
  HSNEntry: undefined,
  SACEntry: undefined,
})

function purchaseUomOption(uom: MasterPurchaseUom): SelectOption {
  const code = (uom.Code ?? '').trim()
  const name = (uom.Name ?? '').trim()
  return {
    value: code,
    label: name && name.toUpperCase() !== code.toUpperCase() ? `${code} - ${name}` : code,
    meta: uom,
  }
}

async function resolveHsnFromChapterId(chapterId: string | undefined): Promise<{
  HSNEntry?: number
  HsnLabel?: string
} | null> {
  const code = (chapterId ?? '').trim()
  if (!code) return null
  const response = await searchHsnCodes(code, 50)
  return pickHsnFromChapterId(code, response.data ?? [])
}

function formatPoCell(value: number | undefined | null): string {
  if (value == null || Number.isNaN(value)) return '—'
  return Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function PurchaseOrderLinesEditor({
  lines,
  onChange,
  defaultWarehouse = '',
  defaultProject = '',
  docType,
  title,
  readOnly = false,
}: PurchaseOrderLinesEditorProps) {
  const isService = isServicePoDocType(docType)
  const resolvedTitle = title ?? (isService ? 'Service Lines' : 'Items')

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingIndex, setEditingIndex] = useState<number | null>(null)
  const [draft, setDraft] = useState<PurchaseOrderLineItem>(emptyLine)
  const [itemLabel, setItemLabel] = useState('')
  const [accountLabel, setAccountLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [taxLabel, setTaxLabel] = useState('')
  const [hsnLabel, setHsnLabel] = useState('')
  const [sacLabel, setSacLabel] = useState('')
  const [projectLabel, setProjectLabel] = useState('')
  const [uomLabel, setUomLabel] = useState('')
  /**
   * Items per unit is typed as text so a decimal like 0.075 survives keystroke-by-keystroke
   * rounding. null means "show the factor derived from the quantities".
   */
  const [itemsPerUnitText, setItemsPerUnitText] = useState<string | null>(null)
  const taxRatesRef = useRef<Record<string, number>>({})
  // The UoM list is per item, but onSearch must stay stable or SearchableSelect refetches forever.
  const draftItemCodeRef = useRef('')
  useEffect(() => {
    draftItemCodeRef.current = draft.ItemCode ?? ''
  }, [draft.ItemCode])

  const itemCodes = useMemo(() => lines.map((line) => line.ItemCode), [lines])
  const itemMap = useItemMasterMap(itemCodes)
  const taxSelected = Boolean((draft.TaxCode ?? '').trim())
  const hsnRequired = !isService && taxSelected
  const sacRequired = isService && taxSelected
  const isEditing = editingIndex != null

  const enrichLine = useCallback((line: PurchaseOrderLineItem): PurchaseOrderLineItem => {
    const item = itemMap[line.ItemCode ?? '']
    const { purchaseUom, stockUom } = resolveLineUoms(line, item)
    const rate = line.TaxCode ? taxRatesRef.current[line.TaxCode] ?? 0 : 0
    return calculateLineTotals(
      {
        ...line,
        UoMCode: purchaseUom,
        UomName: purchaseUom,
        StockUom: stockUom,
        ItemDescription: line.ItemDescription?.trim() || item?.name,
      },
      rate,
    )
  }, [itemMap])

  const rows: LineRow[] = useMemo(
    () => lines.map((line, index) => ({ ...enrichLine(line), __rowIndex: index })),
    [lines, enrichLine],
  )

  const searchItemOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    // Keep list select narrow (ItemCode/ItemName) so SAP $select stays reliable.
    const response = await searchItems(search, 20)
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
      meta: item,
    })).filter((o) => o.value)
  }, [])

  const searchAccountOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchGlAccounts(search)
    return (response.data ?? [])
      .filter((acc) => (acc.Code ?? '').trim() !== PO_TN.forbiddenGlAccount)
      .map((acc) => ({
        value: acc.Code ?? '',
        label: `${acc.Code ?? ''}${acc.Name ? ` - ${acc.Name}` : ''}`.trim(),
      }))
      .filter((o) => o.value)
  }, [])

  const searchPurchaseUomOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const uoms = await listPurchaseUoms(draftItemCodeRef.current, search)
    return uoms.map(purchaseUomOption).filter((o) => o.value)
  }, [])

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: formatWarehouseOptionLabel(wh),
      meta: wh,
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

  const resetDialogLabels = () => {
    setItemLabel('')
    setAccountLabel('')
    setWarehouseLabel('')
    setTaxLabel('')
    setHsnLabel('')
    setSacLabel('')
    setProjectLabel('')
    setUomLabel('')
    setItemsPerUnitText(null)
  }

  const closeDialog = () => {
    setDialogOpen(false)
    setEditingIndex(null)
    setDraft(emptyLine())
    resetDialogLabels()
  }

  const openAddDialog = () => {
    setEditingIndex(null)
    setDraft({
      ...emptyLine(),
      WarehouseCode: isService ? '' : defaultWarehouse,
      ProjectCode: defaultProject,
    })
    resetDialogLabels()
    if (!isService && defaultWarehouse) setWarehouseLabel(defaultWarehouse)
    if (defaultProject) setProjectLabel(defaultProject)
    setDialogOpen(true)
  }

  const openEditDialog = (index: number) => {
    const line = lines[index]
    if (!line) return
    setEditingIndex(index)
    setDraft(enrichLine(line))
    const itemCode = line.ItemCode?.trim()
    setUomLabel(resolvePurchaseUnit(line))
    setItemsPerUnitText(null)
    if (itemCode) {
      void (async () => {
        const meta = await lookupItem(itemCode)
        const stockUom = meta?.InventoryUom || meta?.PurchaseUnit
        setDraft((prev) => {
          if (prev.ItemCode !== itemCode) return prev
          const purchaseUnit = resolvePurchaseUnit(prev) || meta?.PurchaseUnit || ''
          return {
            ...prev,
            StockUom: stockUom || prev.StockUom,
            UoMCode: purchaseUnit || prev.UoMCode,
            UomName: purchaseUnit || prev.UomName,
            MeasureUnit: purchaseUnit || prev.MeasureUnit,
          }
        })
      })()
    }
    setItemLabel(formatCodeWithName(line.ItemCode, line.ItemDescription))
    setAccountLabel(line.AccountLabel ?? line.AccountCode ?? '')
    setWarehouseLabel(line.WarehouseCode ?? '')
    setTaxLabel(line.TaxCode ?? '')
    setHsnLabel(line.HsnLabel ?? (line.HSNEntry != null ? String(line.HSNEntry) : ''))
    setSacLabel(line.SacLabel ?? (line.SACEntry != null ? String(line.SACEntry) : ''))
    setProjectLabel(line.ProjectCode ?? '')
    setDialogOpen(true)
  }

  const handleSaveLine = () => {
    if (isService) {
      if (!draft.AccountCode?.trim()) {
        toast.error('Select a G/L Account.')
        return
      }
      if (draft.AccountCode.trim() === PO_TN.forbiddenGlAccount) {
        toast.error('Selection of G/L Account _SYS00000001265 is not allowed in Purchase Order rows.')
        return
      }
      if (!draft.ItemDescription?.trim()) {
        toast.error('Enter a service description.')
        return
      }
      if (sacRequired && (draft.SACEntry == null || !Number.isFinite(draft.SACEntry))) {
        toast.error('You must select SAC, since GST tax code is selected')
        return
      }
    } else if (!draft.ItemCode) {
      toast.error('Select an item.')
      return
    } else if (!(draft.Quantity != null && draft.Quantity > 0)) {
      toast.error('Purchase Qty must be greater than zero.')
      return
    } else if (!(draft.StockQty != null && draft.StockQty > 0)) {
      toast.error('Stock Qty must be greater than zero.')
      return
    } else if (!resolvePurchaseUnit(draft)) {
      toast.error('Select Purchase UoM.')
      return
    } else if (draft.AccountCode?.trim() === PO_TN.forbiddenGlAccount) {
      toast.error('Selection of G/L Account _SYS00000001265 is not allowed in Purchase Order rows.')
      return
    }

    if (hsnRequired && (draft.HSNEntry == null || !Number.isFinite(draft.HSNEntry))) {
      toast.error('You must select HSN, since GST tax code is selected')
      return
    }

    const nextLine = enrichLine(applyStockPurchaseQty({
      ...draft,
      ItemCode: isService ? undefined : draft.ItemCode,
      WarehouseCode: isService ? undefined : (draft.WarehouseCode || defaultWarehouse),
      UoMCode: isService ? undefined : draft.UoMCode,
      UomName: isService ? undefined : draft.UomName,
      MeasureUnit: isService ? undefined : (resolvePurchaseUnit(draft) || undefined),
      UoMEntry: isService ? undefined : draft.UoMEntry,
      StockUom: isService ? undefined : draft.StockUom,
      StockQty: isService ? undefined : draft.StockQty,
      UnitsOfMeasurment: isService ? undefined : draft.UnitsOfMeasurment,
      HSNEntry: isService ? undefined : draft.HSNEntry,
      AccountCode: draft.AccountCode?.trim() || undefined,
      ProjectCode: draft.ProjectCode || defaultProject || undefined,
      FreeText: draft.FreeText?.trim() || undefined,
    }))

    if (editingIndex != null) {
      const next = [...lines]
      next[editingIndex] = nextLine
      onChange(next)
    } else {
      onChange([...lines, nextLine])
    }
    closeDialog()
  }

  const handleDeleteLine = (index: number) => {
    onChange(lines.filter((_, i) => i !== index))
  }

  const itemColumns: SapColumn<LineRow>[] = [
    {
      key: 'ItemCode',
      header: 'Item',
      accessor: (r) => formatCodeWithName(r.ItemCode, r.ItemDescription ?? itemMap[r.ItemCode ?? '']?.name),
    },
    { key: 'FreeText', header: 'Free Text', accessor: (r) => r.FreeText?.trim() || '—' },
    { key: 'WarehouseCode', header: 'Whse', accessor: (r) => r.WarehouseCode ?? '—' },
    { key: 'LocationCode', header: 'Loc.', accessor: (r) => r.LocationCode != null ? String(r.LocationCode) : '—' },
    { key: 'Quantity', header: 'Purchase Qty', accessor: (r) => r.Quantity },
    { key: 'UoMCode', header: 'Purchase UoM', accessor: (r) => resolvePurchaseUnit(r) || '—' },
    { key: 'StockQty', header: 'Stock Qty', accessor: (r) => r.StockQty ?? '—' },
    { key: 'StockUom', header: 'Stock UoM', accessor: (r) => r.StockUom ?? '—' },
    {
      key: 'UnitsOfMeasurment',
      header: 'Items/Unit',
      accessor: (r) => {
        const factor = r.UnitsOfMeasurment ?? calcItemsPerUnit(r.StockQty, r.Quantity)
        return factor != null ? formatPoCell(factor) : '—'
      },
    },
    {
      key: 'UseBaseUnits',
      header: 'Inventory UoM',
      accessor: (r) => {
        const flag = r.UseBaseUnits
          ?? calcUseBaseUnits(r.UnitsOfMeasurment ?? calcItemsPerUnit(r.StockQty, r.Quantity))
        if (flag === 'tYES') return 'Yes'
        if (flag === 'tNO') return 'No'
        return '—'
      },
    },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'DiscountPercent', header: 'Disc %', accessor: (r) => r.DiscountPercent ?? '—' },
    { key: 'TaxCode', header: 'Tax', accessor: (r) => r.TaxCode ?? '—' },
    {
      key: 'AccountCode',
      header: 'G/L Account',
      accessor: (r) => r.AccountLabel ?? r.AccountCode ?? '—',
    },
    { key: 'HSNEntry', header: 'HSN', accessor: (r) => r.HsnLabel ?? (r.HSNEntry != null ? String(r.HSNEntry) : '—') },
    { key: 'ProjectCode', header: 'Project', accessor: (r) => r.ProjectCode ?? '—' },
    { key: 'TaxableAmount', header: 'Taxable', accessor: (r) => formatPoCell(r.TaxableAmount ?? r.LineTotal) },
    { key: 'GrossTotal', header: 'Gross', accessor: (r) => formatPoCell(r.GrossTotal) },
  ]

  const serviceColumns: SapColumn<LineRow>[] = [
    {
      key: 'AccountCode',
      header: 'G/L Account',
      accessor: (r) => r.AccountLabel ?? r.AccountCode ?? '—',
    },
    { key: 'ItemDescription', header: 'Description', accessor: (r) => r.ItemDescription ?? '—' },
    { key: 'FreeText', header: 'Free Text', accessor: (r) => r.FreeText?.trim() || '—' },
    { key: 'Quantity', header: 'Qty', accessor: (r) => r.Quantity },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'DiscountPercent', header: 'Disc %', accessor: (r) => r.DiscountPercent ?? '—' },
    { key: 'TaxCode', header: 'Tax', accessor: (r) => r.TaxCode ?? '—' },
    { key: 'SACEntry', header: 'SAC', accessor: (r) => r.SacLabel ?? (r.SACEntry != null ? String(r.SACEntry) : '—') },
    { key: 'ProjectCode', header: 'Project', accessor: (r) => r.ProjectCode ?? '—' },
    { key: 'TaxableAmount', header: 'Taxable', accessor: (r) => formatPoCell(r.TaxableAmount ?? r.LineTotal) },
    { key: 'GrossTotal', header: 'Gross', accessor: (r) => formatPoCell(r.GrossTotal) },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold text-slate-700">{resolvedTitle}</h3>
        {!readOnly ? (
          <Button type="button" size="sm" onClick={openAddDialog}>
            <Plus className="mr-1.5 h-4 w-4" />
            {isService ? 'Add Service' : 'Add Item'}
          </Button>
        ) : null}
      </div>

      <SapDataGrid
        columns={isService ? serviceColumns : itemColumns}
        data={rows}
        getRowKey={(row) => row.__rowIndex}
        emptyMessage={isService ? 'No service lines added yet.' : 'No items added yet.'}
        actions={readOnly
          ? undefined
          : (row) => (
              <RowActions>
                <RowActionButton
                  title={isService ? 'Edit service line' : 'Edit item'}
                  icon={<Pencil className={rowActionIconClassName} />}
                  onClick={() => openEditDialog(row.__rowIndex)}
                />
                <RowActionButton
                  title={isService ? 'Delete service line' : 'Delete item'}
                  variant="danger"
                  icon={<Trash2 className={rowActionIconClassName} />}
                  onClick={() => handleDeleteLine(row.__rowIndex)}
                />
              </RowActions>
            )}
      />

      <Modal
        isOpen={dialogOpen}
        onClose={closeDialog}
        title={isEditing
          ? (isService ? 'Edit Service Line' : 'Edit Item')
          : (isService ? 'Add Service Line' : 'Add Item')}
        description={isService
          ? 'Enter G/L account, description, and amounts for this service line.'
          : 'Add a stock/item line to the purchase order.'}
        size="2xl"
        footer={(
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={closeDialog}>
              Cancel
            </Button>
            <Button type="button" onClick={handleSaveLine}>
              {isEditing ? 'Save Changes' : (isService ? 'Add Service' : 'Add Item')}
            </Button>
          </div>
        )}
      >
        <div className="grid gap-4 md:grid-cols-2">
          {isService ? (
            <>
              <SearchableSelect
                label="G/L Account *"
                value={draft.AccountCode ?? ''}
                selectedLabel={accountLabel}
                placeholder="Search G/L account..."
                onSearch={searchAccountOptions}
                onChange={(code, option) => {
                  setAccountLabel(option?.label ?? code)
                  setDraft({
                    ...draft,
                    AccountCode: code,
                    AccountLabel: option?.label ?? code,
                  })
                }}
              />
              <Input
                label="Description *"
                value={draft.ItemDescription ?? ''}
                onChange={(e) => setDraft({ ...draft, ItemDescription: e.target.value })}
              />
            </>
          ) : (
            <>
              <SearchableSelect
                label="Item"
                lookupKind="item"
                value={draft.ItemCode ?? ''}
                selectedLabel={itemLabel}
                placeholder="Search item..."
                onSearch={searchItemOptions}
                onChange={(code, option) => {
                  const label = option?.label ?? code
                  const metaItem = option?.meta as MasterItem | undefined
                  const fromMeta = (metaItem?.ItemName ?? '').trim()
                  const fromLabel = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ').trim() : ''
                  const description = fromMeta || fromLabel
                  setItemLabel(label)
                  setDraft({
                    ...draft,
                    ItemCode: code,
                    ItemDescription: description || undefined,
                    // UoMs are master-driven; drop the previous item's values before refetching.
                    UoMCode: undefined,
                    UomName: undefined,
                    MeasureUnit: undefined,
                    UoMEntry: undefined,
                    StockUom: undefined,
                    WarehouseCode: draft.WarehouseCode || defaultWarehouse,
                    ProjectCode: draft.ProjectCode || defaultProject,
                  })
                  setUomLabel('')
                  setItemsPerUnitText(null)
                  void (async () => {
                    const [meta, uoms] = await Promise.all([
                      lookupItem(code).then((found) => found ?? (option?.meta as MasterItem | undefined)),
                      listPurchaseUoms(code),
                    ])
                    if (!meta) return
                    const purchaseUom = meta.PurchaseUnit || meta.InventoryUom || ''
                    const stockUom = meta.InventoryUom || meta.PurchaseUnit || ''
                    // The default unit must come from the list so items on a real UoM group keep the
                    // UoMEntry SAP expects; the item master alone cannot supply it.
                    const defaultUom = uoms.find((uom) => uom.IsDefault)
                      ?? uoms.find((uom) => (uom.Code ?? '').toUpperCase() === purchaseUom.toUpperCase())
                    const unit = defaultUom?.Code || purchaseUom
                    const masterItemsPerUnit = meta.PurchaseItemsPerUnit != null && meta.PurchaseItemsPerUnit > 0
                      ? meta.PurchaseItemsPerUnit
                      : 1
                    const itemsPerUnit = defaultUom?.ItemsPerUnit != null && defaultUom.ItemsPerUnit > 0
                      ? defaultUom.ItemsPerUnit
                      : masterItemsPerUnit
                    const purchaseQty = draft.Quantity && draft.Quantity > 0 ? draft.Quantity : 1
                    const stockQty = purchaseQty * itemsPerUnit
                    const taxCode = draft.TaxCode || meta.PurchaseVatGroup || ''
                    const warehouse = draft.WarehouseCode || meta.DefaultWarehouse || defaultWarehouse || ''
                    if (warehouse) setWarehouseLabel(warehouse)
                    if (defaultUom) setUomLabel(purchaseUomOption(defaultUom).label)
                    else if (unit) setUomLabel(unit)
                    setDraft((prev) => (
                      prev.ItemCode === code
                        ? applyStockPurchaseQty({
                            ...prev,
                            ItemDescription: prev.ItemDescription || meta.ItemName || description,
                            UoMCode: unit || prev.UoMCode,
                            UomName: unit || prev.UomName,
                            MeasureUnit: unit || prev.MeasureUnit,
                            UoMEntry: defaultUom?.UoMEntry,
                            StockUom: stockUom || prev.StockUom,
                            Quantity: purchaseQty,
                            StockQty: stockQty,
                            UnitsOfMeasurment: itemsPerUnit,
                            WeightKg: meta.InventoryWeight ?? prev.WeightKg,
                            TaxCode: taxCode || prev.TaxCode,
                            WarehouseCode: warehouse || prev.WarehouseCode,
                          })
                        : prev
                    ))
                    if (warehouse) {
                      const loc = await resolveWarehouseLocation(warehouse)
                      if (loc != null) {
                        setDraft((prev) => (prev.ItemCode === code ? { ...prev, LocationCode: loc } : prev))
                      }
                    }
                    if (meta.PurchaseVatGroup) setTaxLabel(meta.PurchaseVatGroup)
                    const chapterId = meta.ChapterID?.trim()
                    if (chapterId) {
                      const hsn = await resolveHsnFromChapterId(chapterId)
                      if (!hsn) return
                      setHsnLabel(hsn.HsnLabel ?? '')
                      setDraft((prev) => (
                        prev.ItemCode === code
                          ? { ...prev, HSNEntry: hsn.HSNEntry, HsnLabel: hsn.HsnLabel }
                          : prev
                      ))
                    }
                  })()
                }}
              />
              <Input
                label="Description"
                value={draft.ItemDescription ?? ''}
                onChange={(e) => setDraft({ ...draft, ItemDescription: e.target.value })}
              />
              <div className="md:col-span-2">
                <Textarea
                  label="Free Text"
                  value={draft.FreeText ?? ''}
                  onChange={(e) => setDraft({ ...draft, FreeText: e.target.value })}
                />
              </div>
              <SearchableSelect
                label="Warehouse"
                value={draft.WarehouseCode ?? ''}
                selectedLabel={warehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                onChange={(code, option) => {
                  const loc = (option?.meta as MasterWarehouse | undefined)?.Location
                  setWarehouseLabel(option?.label ?? code)
                  setDraft({
                    ...draft,
                    WarehouseCode: code,
                    LocationCode: loc != null && Number.isFinite(loc) ? loc : undefined,
                  })
                }}
              />
              <Input
                label="Purchase Qty *"
                type="number"
                min="0"
                nonNegative
                value={String(draft.Quantity ?? 0)}
                onChange={(e) => {
                  setItemsPerUnitText(null)
                  setDraft(withPurchaseQty(draft, Number(e.target.value)))
                }}
                required
              />
              <SearchableSelect
                label="Purchase UoM *"
                value={resolvePurchaseUnit(draft)}
                selectedLabel={uomLabel}
                placeholder={draft.ItemCode ? 'Search purchase UoM...' : 'Select an item first'}
                onSearch={searchPurchaseUomOptions}
                disabled={!draft.ItemCode}
                onChange={(value, option) => {
                  const uom = option?.meta as MasterPurchaseUom | undefined
                  setUomLabel(option?.label ?? value)
                  setItemsPerUnitText(null)
                  setDraft((prev) => {
                    const next = {
                      ...prev,
                      UoMCode: value,
                      UomName: value,
                      MeasureUnit: value,
                      UoMEntry: uom?.UoMEntry,
                    }
                    // A unit from a UoM group brings its own conversion; otherwise keep what is there.
                    return uom?.ItemsPerUnit != null && uom.ItemsPerUnit > 0
                      ? withItemsPerUnit(next, uom.ItemsPerUnit)
                      : next
                  })
                }}
              />
              <Input
                label="Stock Qty *"
                type="number"
                min="0"
                nonNegative
                value={String(draft.StockQty ?? 0)}
                onChange={(e) => {
                  setItemsPerUnitText(null)
                  setDraft(withStockQty(draft, Number(e.target.value)))
                }}
                required
              />
              <Input
                label="Stock UoM"
                value={draft.StockUom ?? ''}
                disabled
              />
              <Input
                label="Items per Unit"
                type="number"
                min="0"
                step="any"
                nonNegative
                value={itemsPerUnitText ?? (() => {
                  const factor = draft.UnitsOfMeasurment ?? calcItemsPerUnit(draft.StockQty, draft.Quantity)
                  return factor != null ? String(Number(factor.toFixed(6))) : ''
                })()}
                onChange={(e) => {
                  const text = e.target.value
                  setItemsPerUnitText(text)
                  const factor = Number(text)
                  if (text !== '' && Number.isFinite(factor)) setDraft(withItemsPerUnit(draft, factor))
                }}
              />
              <Input
                label="Inventory UoM"
                value={(() => {
                  const flag = draft.UseBaseUnits
                    ?? calcUseBaseUnits(draft.UnitsOfMeasurment ?? calcItemsPerUnit(draft.StockQty, draft.Quantity))
                  if (flag === 'tYES') return 'Yes'
                  if (flag === 'tNO') return 'No'
                  return ''
                })()}
                readOnly
              />
              <SearchableSelect
                label="G/L Account"
                value={draft.AccountCode ?? ''}
                selectedLabel={accountLabel}
                placeholder="Determined by SAP"
                onSearch={searchAccountOptions}
                disabled
                onChange={() => undefined}
              />
            </>
          )}
          {isService ? (
            <>
              <div className="md:col-span-2">
                <Textarea
                  label="Free Text"
                  value={draft.FreeText ?? ''}
                  onChange={(e) => setDraft({ ...draft, FreeText: e.target.value })}
                />
              </div>
              <Input
                label="Quantity"
                type="number"
                min="0"
                nonNegative
                value={String(draft.Quantity ?? 0)}
                onChange={(e) => setDraft({ ...draft, Quantity: Number(e.target.value) })}
                required
              />
            </>
          ) : null}
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
          {!isService ? (
            <SearchableSelect
              label={hsnRequired ? 'HSN *' : 'HSN'}
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
          ) : (
            <SearchableSelect
              label={sacRequired ? 'SAC *' : 'SAC'}
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
          )}
          <SearchableSelect
            label="Project"
            lookupKind="project"
            value={draft.ProjectCode ?? ''}
            selectedLabel={projectLabel}
            placeholder="Search project by code or name..."
            onSearch={searchProjectOptions}
            onChange={(code, option) => {
              setProjectLabel(option?.label ?? code)
              setDraft({ ...draft, ProjectCode: code })
            }}
          />
        </div>
      </Modal>
    </div>
  )
}
