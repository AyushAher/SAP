import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { SelectableSapDataGrid } from '@/Components/shared/SelectableSapDataGrid'
import type { SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Card, CardContent, Input, SearchableSelect, Select } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName, resolveMasterSelectLabels, resolveProject } from '@/helpers/masterLookup'
import { applyProductionCategoryDefaults, validateProductionOrderForm } from '@/helpers/productionOrderForm'
import { toast } from '@/helpers/toast'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import {
  createProductionOrder,
  downloadProductionOrderPdf,
  getProductionOrder,
  updateProductionOrder,
} from '@/Requests/productionOrders'
import {
  listSalesOrders,
  searchCustomers,
  searchItems,
  searchWarehouses,
  formatWarehouseOptionLabel,
  type MasterSalesOrder,
} from '@/Requests/masters'
import type { SelectOption } from '@/types'
import type { ProductionOrder, ProductionOrderLine } from '@/types/production'

const STATUS_OPTIONS = [
  { value: 'boposPlanned', label: 'Planned' },
  { value: 'boposReleased', label: 'Released' },
  { value: 'boposClosed', label: 'Closed' },
  { value: 'boposCancelled', label: 'Cancelled' },
]

// SAP's BoProductionOrderTypeEnum, all three members.
const TYPE_OPTIONS = [
  { value: 'bopotStandard', label: 'Standard' },
  { value: 'bopotSpecial', label: 'Special' },
  { value: 'bopotDisassembly', label: 'Disassembly' },
]

const CATEGORY_OPTIONS = [
  { value: 'JOB', label: 'JOB - Sub-Contractor Location' },
  { value: 'EXT', label: 'EXT - Customer Site' },
  { value: 'INT', label: 'INT - Factory' },
]

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

function asDateInputValue(value: unknown): string {
  return String(value ?? '').slice(0, 10)
}

export function ProductionOrderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [form, setForm] = useState<ProductionOrder>({
    ItemNumber: '',
    PlannedQuantity: 0,
    Warehouse: '',
    IssWarehouse: '',
    Project: '',
    Status: 'boposPlanned',
    Type: 'bopotStandard',
    ProductionCategory: 'JOB',
    PostingDate: today(),
    StartDate: today(),
    DueDate: today(),
    ProductionOrderLines: [],
  })
  const [lines, setLines] = useState<ProductionOrderLine[]>([])
  const [draftLine, setDraftLine] = useState<ProductionOrderLine>({ ItemNo: '', PlannedQuantity: 0 })
  const [draftItemLabel, setDraftItemLabel] = useState('')
  const [customerLabel, setCustomerLabel] = useState('')
  const [itemLabel, setItemLabel] = useState('')
  const [projectName, setProjectName] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [issWarehouseLabel, setIssWarehouseLabel] = useState('')
  const [salesOrderLabel, setSalesOrderLabel] = useState('')
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [lineReviewWarning, setLineReviewWarning] = useState(false)
  // Rows from the last sales order search, so a pick can resolve customer, project and DocEntry.
  const salesOrderRows = useRef<MasterSalesOrder[]>([])

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
      label: formatWarehouseOptionLabel(wh),
    })).filter((o) => o.value)
  }, [])

  const searchSalesOrderOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await listSalesOrders(search, form.CustomerCode)
    salesOrderRows.current = response.data ?? []
    return salesOrderRows.current.map((so) => ({
      value: String(so.DocNum ?? so.DocEntry ?? ''),
      label: `${so.DocNum ?? so.DocEntry ?? ''}${so.NumAtCard ? ` - ${so.NumAtCard}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [form.CustomerCode])

  useEffect(() => {
    if (!id) return
    getProductionOrder(id)
      .then(async (po) => {
        // Legacy seeded the issuing warehouse from the last component line; SAP does not store it.
        const issWarehouse = po.IssWarehouse ?? po.ProductionOrderLines?.at(-1)?.Warehouse ?? ''
        setForm({ ...po, IssWarehouse: issWarehouse })
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
        if (po.ProjectName) setProjectName(po.ProjectName)
        else if (po.Project) setProjectName((await resolveProject(po.Project))?.Name ?? '')
        if (po.Warehouse) setWarehouseLabel(String(po.Warehouse))
        if (issWarehouse) setIssWarehouseLabel(String(issWarehouse))
        if (po.SalesOrderDocNum) setSalesOrderLabel(String(po.SalesOrderDocNum))
      })
      .catch((e: unknown) => {
        const message = e instanceof Error ? e.message : 'Production order could not be loaded.'
        setError(message)
        setLoadFailed(true)
        toast.error(message)
      })
      .finally(() => setLoading(false))
  }, [id])

  const lineItemCodes = useMemo(() => lines.map((line) => line.ItemNo), [lines])
  const lineItemMap = useItemMasterMap(lineItemCodes)

  const handleSalesOrderChange = async (value: string) => {
    setSalesOrderLabel(value)
    const picked = salesOrderRows.current.find((so) => String(so.DocNum ?? so.DocEntry ?? '') === value)
    const project = picked?.Project ?? ''
    setForm((prev) => ({
      ...prev,
      SalesOrderDocNum: picked?.DocNum ?? (value ? Number(value) : undefined),
      SalesOrderDocEntry: picked?.DocEntry,
      CustomerCode: picked?.CardCode ?? prev.CustomerCode,
      CustomerName: picked?.CardName ?? prev.CustomerName,
      Project: project,
    }))
    if (picked?.CardCode) setCustomerLabel(formatCodeWithName(picked.CardCode, picked.CardName))
    setProjectName(project ? (await resolveProject(project))?.Name ?? '' : '')
  }

  const handlePlannedQuantityChange = (value: number) => {
    setForm((prev) => ({ ...prev, PlannedQuantity: value }))
    if (!lines.length) return
    if (!lineReviewWarning) {
      toast.info('Header planned quantity changed. Review the component line quantities before saving.')
    }
    setLineReviewWarning(true)
  }

  const updateLine = (index: number, patch: Partial<ProductionOrderLine>) => {
    setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)))
  }

  const handleAddLine = () => {
    if (!draftLine.ItemNo) {
      setError('Select a line item before adding it.')
      return
    }
    const nextLine: ProductionOrderLine = {
      ...draftLine,
      LineNumber: (lines.reduce((max, line) => Math.max(max, line.LineNumber ?? 0), 0)) + 1,
      DocumentAbsoluteEntry: form.AbsoluteEntry,
      Warehouse: draftLine.Warehouse ?? form.IssWarehouse ?? '',
    }
    setLines((prev) => [...prev, nextLine])
    setDraftLine({ ItemNo: '', PlannedQuantity: 0 })
    setDraftItemLabel('')
    setError(null)
  }

  const lineColumns: SapColumn<ProductionOrderLine>[] = [
    {
      key: 'ItemNo',
      header: 'Item',
      render: (row) => {
        const index = lines.indexOf(row)
        return (
          <SearchableSelect
            lookupKind="item"
            value={row.ItemNo ?? ''}
            selectedLabel={formatCodeWithName(row.ItemNo, row.ItemName ?? lineItemMap[row.ItemNo ?? '']?.name)}
            placeholder="Search item..."
            onSearch={searchItemOptions}
            onChange={(code, option) => {
              const label = option?.label ?? code
              updateLine(index, {
                ItemNo: code,
                ItemName: label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : undefined,
              })
            }}
          />
        )
      },
    },
    {
      key: 'PlannedQuantity',
      header: 'Planned Qty',
      render: (row) => {
        const index = lines.indexOf(row)
        return (
          <Input
            type="number"
            nonNegative
            value={String(row.PlannedQuantity ?? 0)}
            onChange={(e) => updateLine(index, { PlannedQuantity: Number(e.target.value) })}
          />
        )
      },
    },
    { key: 'IssuedQuantity', header: 'Issued Qty', accessor: (r) => r.IssuedQuantity ?? 0 },
    { key: 'Warehouse', header: 'Warehouse', accessor: (r) => r.Warehouse },
  ]

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    // New lines inherit the issuing warehouse, the way the legacy form seeded them on add.
    const submittedLines = lines.map((line) => ({
      ...line,
      Warehouse: line.Warehouse || form.IssWarehouse || '',
    }))
    const validationError = validateProductionOrderForm(form, submittedLines)
    if (validationError) {
      setError(validationError)
      toast.error(validationError)
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload: ProductionOrder = {
        ...form,
        ProductionOrderLines: submittedLines,
        PostingDate: form.PostingDate ?? today(),
        DueDate: form.DueDate ?? today(),
      }
      const result = id
        ? await updateProductionOrder(Number(id), payload)
        : await createProductionOrder(payload)

      // Above-threshold orders are stored as approval requests and are not in SAP yet.
      if (result?.pendingApproval) {
        const message = id
          ? 'Production order update submitted for approval. It will reach SAP after approval.'
          : 'Production order submitted for approval. It will appear in SAP after approval.'
        toast.info(message)
        navigate(ROUTES.MY_APPROVAL_REQUESTS, {
          state: { message, approvalRequestId: result.pendingApprovalRequestId },
        })
        return
      }

      const docNum = result?.DocumentNumber ?? form.DocumentNumber
      const subject = docNum ? `Production order ${docNum}` : 'Production order'
      toast.success(id ? `${subject} updated in SAP.` : `${subject} created in SAP.`)
      navigate(ROUTES.PRODUCTION_ORDERS)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Save failed'
      setError(message)
      toast.error(message)
    } finally {
      setSaving(false)
    }
  }

  const handleDownloadPdf = async () => {
    if (!id) return
    setError(null)
    try {
      await downloadProductionOrderPdf(Number(id), form.DocumentNumber)
    } catch (err) {
      const message = err instanceof Error
        ? err.message
        : 'The production order PDF could not be downloaded.'
      setError(message)
      toast.error(message)
    }
  }

  if (loading) return <div className="py-12 text-center">Loading...</div>

  if (loadFailed) {
    return (
      <div className="space-y-6">
        <PageHeader title="Production Order" />
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">
          {error ?? 'Production order could not be loaded.'}
        </div>
        <Button variant="outline" onClick={() => navigate(ROUTES.PRODUCTION_ORDERS)}>Back to Production Orders</Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={id ? `Update Production Order #${form.DocumentNumber ?? id}` : 'New Production Order'}
        action={id ? (
          <Button type="button" variant="outline" onClick={() => void handleDownloadPdf()}>
            Download PDF
          </Button>
        ) : undefined}
      />
      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{error}</div>}
      {lineReviewWarning && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800" role="status">
          The header planned quantity changed. Component line quantities were left as they were — review them before saving.
        </div>
      )}
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
                required
                value={String(form.SalesOrderDocNum ?? '')}
                selectedLabel={salesOrderLabel}
                placeholder="Search sales order..."
                onSearch={searchSalesOrderOptions}
                onChange={(value) => void handleSalesOrderChange(value)}
              />
              <Input
                label="Project Code"
                value={form.Project ?? ''}
                readOnly
                hint="Taken from the selected sales order."
                placeholder="Select a sales order"
              />
              <Input label="Project Name" value={projectName} readOnly placeholder="Select a sales order" />
              <SearchableSelect
                label="Product No."
                lookupKind="item"
                required
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
                  const applied = applyProductionCategoryDefaults(value, form, lines)
                  setForm(applied.order)
                  setLines(applied.lines)
                  setWarehouseLabel(applied.order.Warehouse ?? '')
                  setIssWarehouseLabel(applied.order.IssWarehouse ?? '')
                }}
                options={CATEGORY_OPTIONS}
              />
              <Input
                label="Planned Qty"
                type="number"
                nonNegative
                required
                value={String(form.PlannedQuantity ?? 0)}
                onChange={(e) => handlePlannedQuantityChange(Number(e.target.value))}
              />
              <Input
                label="Start Date"
                type="date"
                value={asDateInputValue(form.StartDate)}
                onChange={(e) => setForm({ ...form, StartDate: e.target.value })}
              />
              <Input
                label="Due Date"
                type="date"
                required
                value={asDateInputValue(form.DueDate)}
                onChange={(e) => setForm({ ...form, DueDate: e.target.value })}
              />
              <SearchableSelect
                label="Receipt Warehouse"
                required
                value={form.Warehouse ?? ''}
                selectedLabel={warehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                onChange={(code, option) => {
                  setWarehouseLabel(option?.label ?? code)
                  setForm({ ...form, Warehouse: code })
                }}
              />
              <SearchableSelect
                label="Issuing Warehouse"
                required
                value={form.IssWarehouse ?? ''}
                selectedLabel={issWarehouseLabel}
                placeholder="Search warehouse..."
                onSearch={searchWarehouseOptions}
                hint="Seeds the warehouse of every component line."
                onChange={(code, option) => {
                  setIssWarehouseLabel(option?.label ?? code)
                  setForm({ ...form, IssWarehouse: code })
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
              <Input label="Line Planned Qty" type="number" nonNegative value={String(draftLine.PlannedQuantity ?? 0)} onChange={(e) => setDraftLine({ ...draftLine, PlannedQuantity: Number(e.target.value) })} />
              <div className="flex items-end">
                <Button type="button" variant="outline" onClick={handleAddLine}>Add Line</Button>
              </div>
            </div>

            <SelectableSapDataGrid
              toolbarTitle="Production Order Lines"
              columns={lineColumns}
              data={lines}
              getRowKey={(row) => row.LineNumber ?? lines.indexOf(row)}
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
