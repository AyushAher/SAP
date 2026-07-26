import { useCallback, useMemo, useRef, useState } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import {
  RowActionButton,
  RowActions,
  rowActionIconClassName,
} from '@/Components/shared/RowActions'
import { Button, Input, Modal, SearchableSelect } from '@/Components/ui'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { calculateLineTotals } from '@/helpers/purchaseOrderForm'
import { isServicePoDocType, PO_TN } from '@/helpers/purchaseOrderTnValidation'
import { toast } from '@/helpers/toast'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import {
  ITEM_DETAIL_FIELDS,
  searchGlAccounts,
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
  /** SAP DocType: dDocument_Items | dDocument_Service */
  docType?: string
  /** When JOB, Production Order No is required on each line (TN). */
  requireProdNo?: boolean
  title?: string
  readOnly?: boolean
}

type LineRow = PurchaseOrderLineItem & { __rowIndex: number }

const emptyLine = (): PurchaseOrderLineItem => ({
  ItemCode: '',
  ItemDescription: '',
  Quantity: 0,
  UnitPrice: 0,
  DiscountPercent: undefined,
  TaxCode: '',
  WarehouseCode: '',
  ProjectCode: '',
  CostingCode: '',
  AccountCode: '',
  HSNEntry: undefined,
  SACEntry: undefined,
  U_ProdNo: '',
})

async function resolveHsnFromChapterId(chapterId: string | undefined): Promise<{
  HSNEntry?: number
  HsnLabel?: string
} | null> {
  const code = (chapterId ?? '').trim()
  if (!code) return null
  const response = await searchHsnCodes(code, 20)
  const rows = response.data ?? []
  const exact = rows.find((h) => (h.ChapterID ?? '').trim() === code)
    ?? rows.find((h) => (h.DisplayLabel ?? '').includes(code))
    ?? rows[0]
  if (exact?.AbsEntry == null) return null
  return {
    HSNEntry: exact.AbsEntry,
    HsnLabel: exact.DisplayLabel ?? String(exact.AbsEntry),
  }
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
  requireProdNo = false,
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
  const taxRatesRef = useRef<Record<string, number>>({})

  const itemCodes = useMemo(() => lines.map((line) => line.ItemCode), [lines])
  const itemMap = useItemMasterMap(itemCodes)
  const taxSelected = Boolean((draft.TaxCode ?? '').trim())
  const hsnRequired = !isService && taxSelected
  const sacRequired = isService && taxSelected
  const isEditing = editingIndex != null

  const enrichLine = useCallback((line: PurchaseOrderLineItem): PurchaseOrderLineItem => {
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
  }, [itemMap])

  const rows: LineRow[] = useMemo(
    () => lines.map((line, index) => ({ ...enrichLine(line), __rowIndex: index })),
    [lines, enrichLine],
  )

  const searchItemOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(search, 20, ITEM_DETAIL_FIELDS)
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

  const resetDialogLabels = () => {
    setItemLabel('')
    setAccountLabel('')
    setWarehouseLabel('')
    setTaxLabel('')
    setHsnLabel('')
    setSacLabel('')
    setProjectLabel('')
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
    setDraft({ ...line })
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
    }

    if (requireProdNo && !draft.U_ProdNo?.trim()) {
      toast.error('Production Order No is required for JOB purchase orders.')
      return
    }
    if (hsnRequired && (draft.HSNEntry == null || !Number.isFinite(draft.HSNEntry))) {
      toast.error('You must select HSN, since GST tax code is selected')
      return
    }

    const nextLine = enrichLine({
      ...draft,
      ItemCode: isService ? undefined : draft.ItemCode,
      WarehouseCode: isService ? undefined : (draft.WarehouseCode || defaultWarehouse),
      UoMCode: isService ? undefined : draft.UoMCode,
      UomName: isService ? undefined : draft.UomName,
      HSNEntry: isService ? undefined : draft.HSNEntry,
      AccountCode: isService ? draft.AccountCode : undefined,
      ProjectCode: draft.ProjectCode || defaultProject || undefined,
    })

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
    { key: 'WarehouseCode', header: 'Whse', accessor: (r) => r.WarehouseCode ?? '—' },
    { key: 'Quantity', header: 'Qty', accessor: (r) => r.Quantity },
    { key: 'UoMCode', header: 'UoM', accessor: (r) => r.UoMCode ?? r.UomName ?? itemMap[r.ItemCode ?? '']?.uom ?? '—' },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'DiscountPercent', header: 'Disc %', accessor: (r) => r.DiscountPercent ?? '—' },
    { key: 'TaxCode', header: 'Tax', accessor: (r) => r.TaxCode ?? '—' },
    { key: 'HSNEntry', header: 'HSN', accessor: (r) => r.HsnLabel ?? (r.HSNEntry != null ? String(r.HSNEntry) : '—') },
    { key: 'ProjectCode', header: 'Project', accessor: (r) => r.ProjectCode ?? '—' },
    { key: 'U_ProdNo', header: 'Prod Order No', accessor: (r) => r.U_ProdNo ?? '—' },
    { key: 'CostingCode', header: 'Cost Ctr', accessor: (r) => r.CostingCode ?? '—' },
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
    { key: 'Quantity', header: 'Qty', accessor: (r) => r.Quantity },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoCell(r.UnitPrice) },
    { key: 'DiscountPercent', header: 'Disc %', accessor: (r) => r.DiscountPercent ?? '—' },
    { key: 'TaxCode', header: 'Tax', accessor: (r) => r.TaxCode ?? '—' },
    { key: 'SACEntry', header: 'SAC', accessor: (r) => r.SacLabel ?? (r.SACEntry != null ? String(r.SACEntry) : '—') },
    { key: 'ProjectCode', header: 'Project', accessor: (r) => r.ProjectCode ?? '—' },
    { key: 'U_ProdNo', header: 'Prod Order No', accessor: (r) => r.U_ProdNo ?? '—' },
    { key: 'CostingCode', header: 'Cost Ctr', accessor: (r) => r.CostingCode ?? '—' },
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
                  const description = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
                  const meta = option?.meta as MasterItem | undefined
                  const uom = meta?.PurchaseUnit || meta?.InventoryUom || ''
                  const taxCode = draft.TaxCode || meta?.PurchaseVatGroup || ''
                  setItemLabel(label)
                  setDraft({
                    ...draft,
                    ItemCode: code,
                    ItemDescription: description,
                    UoMCode: uom || undefined,
                    UomName: uom || undefined,
                    WeightKg: meta?.InventoryWeight ?? 0,
                    TaxCode: taxCode,
                    WarehouseCode: draft.WarehouseCode || defaultWarehouse,
                    ProjectCode: draft.ProjectCode || defaultProject,
                  })
                  if (meta?.PurchaseVatGroup) setTaxLabel(meta.PurchaseVatGroup)
                  const chapterId = meta?.ChapterID?.trim()
                  if (chapterId) {
                    void resolveHsnFromChapterId(chapterId).then((hsn) => {
                      if (!hsn) return
                      setHsnLabel(hsn.HsnLabel ?? '')
                      setDraft((prev) => (
                        prev.ItemCode === code
                          ? { ...prev, HSNEntry: hsn.HSNEntry, HsnLabel: hsn.HsnLabel }
                          : prev
                      ))
                    })
                  }
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
            </>
          )}
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
            placeholder="Search project..."
            onSearch={searchProjectOptions}
            onChange={(code, option) => {
              setProjectLabel(option?.label ?? code)
              setDraft({ ...draft, ProjectCode: code })
            }}
          />
          <Input
            label={requireProdNo ? 'Prod Order No *' : 'Prod Order No'}
            value={draft.U_ProdNo ?? ''}
            onChange={(e) => setDraft({ ...draft, U_ProdNo: e.target.value })}
            required={requireProdNo}
          />
          <Input
            label="Cost Center"
            value={draft.CostingCode ?? ''}
            onChange={(e) => setDraft({ ...draft, CostingCode: e.target.value })}
          />
        </div>
      </Modal>
    </div>
  )
}
