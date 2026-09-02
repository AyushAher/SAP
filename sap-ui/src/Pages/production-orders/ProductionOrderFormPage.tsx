import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Layers } from 'lucide-react'
import { ProductionOrderSubassembliesCard } from '@/Components/production/ProductionOrderSubassembliesCard'
import { PageHeader } from '@/Components/shared/PageHeader'
import {
  Button,
  Card,
  CardContent,
  Input,
  SapDateInput,
  SearchableSelect,
  Select,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
  Textarea,
} from '@/Components/ui'
import { ROUTES, productionOrderSubassemblyPath } from '@/config/constants'
import { todayIsoDate, toIsoDateOnly } from '@/helpers/lib/utils'
import { formatCodeWithName, nameFromCodeWithNameLabel, resolveMasterSelectLabels, resolveProject } from '@/helpers/masterLookup'
import {
  applyProductionCategoryDefaults,
  filterSalesOrderProducts,
  isItemOnSalesOrder,
  salesOrderPlannedQtyCap,
  validateProductionOrderForm,
} from '@/helpers/productionOrderForm'
import {
  clearCreateDraft,
  loadCreateDraft,
  removeDraftSubassembly,
  saveCreateDraftHeader,
} from '@/helpers/productionOrderCreateDraft'
import { toast } from '@/helpers/toast'
import {
  createProductionOrder,
  downloadProductionOrderPdf,
  getProductionOrder,
  listSubassemblies,
  updateProductionOrder,
} from '@/Requests/productionOrders'
import {
  listSalesOrders,
  getSalesOrder,
  searchCustomers,
  type MasterSalesOrder,
} from '@/Requests/masters'
import type { SelectOption } from '@/types'
import { PRODUCTION_ORDER_TYPE_SPECIAL, type ProductionOrder, type SalesOrderProductLine } from '@/types/production'

const STATUS_OPTIONS = [
  { value: 'boposPlanned', label: 'Planned' },
  { value: 'boposReleased', label: 'Released' },
  { value: 'boposClosed', label: 'Closed' },
  { value: 'boposCancelled', label: 'Cancelled' },
]

const CATEGORY_OPTIONS = [
  { value: 'JOB', label: 'JOB - Sub-Contractor Location' },
  { value: 'EXT', label: 'EXT - Customer Site' },
  { value: 'INT', label: 'INT - Factory' },
]

export function ProductionOrderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const isDraftParent = !id || id === ROUTES.PRODUCTION_ORDER_DRAFT_ID
  const jobDefaults = applyProductionCategoryDefaults('JOB', {
    ItemNumber: '',
    PlannedQuantity: 0,
    Project: '',
    Status: 'boposPlanned',
    Type: PRODUCTION_ORDER_TYPE_SPECIAL,
    ProductionCategory: 'JOB',
    PostingDate: todayIsoDate(),
    CreationDate: todayIsoDate(),
    StartDate: todayIsoDate(),
    DueDate: todayIsoDate(),
  }, [])
  const [form, setForm] = useState<ProductionOrder>(jobDefaults.order)
  const [customerLabel, setCustomerLabel] = useState('')
  const [itemLabel, setItemLabel] = useState('')
  const [projectName, setProjectName] = useState('')
  const [salesOrderLabel, setSalesOrderLabel] = useState('')
  const [salesOrderProducts, setSalesOrderProducts] = useState<SalesOrderProductLine[] | null>(null)
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [lineReviewWarning, setLineReviewWarning] = useState(false)
  const [draftSubassemblies, setDraftSubassemblies] = useState<ProductionOrder[]>([])
  // Rows from the last sales order search, so a pick can resolve customer, project and DocEntry.
  const salesOrderRows = useRef<MasterSalesOrder[]>([])

  const searchCustomerOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchCustomers(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchProductOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    return filterSalesOrderProducts(salesOrderProducts ?? [], search).map((item) => ({
      value: item.ItemCode ?? '',
      label: formatCodeWithName(item.ItemCode, item.ItemName),
    })).filter((o) => o.value)
  }, [salesOrderProducts])

  const searchSalesOrderOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await listSalesOrders(search, form.CustomerCode)
    salesOrderRows.current = response.data ?? []
    return salesOrderRows.current.map((so) => ({
      value: String(so.DocNum ?? so.DocEntry ?? ''),
      label: `${so.DocNum ?? so.DocEntry ?? ''}${so.NumAtCard ? ` - ${so.NumAtCard}` : ''}`.trim(),
    })).filter((o) => o.value)
  }, [form.CustomerCode])

  useEffect(() => {
    if (!isDraftParent) return
    const draft = loadCreateDraft()
    if (!draft) return
    const applied = applyProductionCategoryDefaults(
      draft.header.ProductionCategory ?? 'JOB',
      { ...jobDefaults.order, ...draft.header, Type: PRODUCTION_ORDER_TYPE_SPECIAL },
      [],
    )
    setForm({
      ...applied.order,
      Warehouse: draft.header.Warehouse || applied.order.Warehouse,
      IssWarehouse: draft.header.IssWarehouse || applied.order.IssWarehouse,
    })
    setCustomerLabel(draft.labels?.customerLabel ?? '')
    setItemLabel(draft.labels?.itemLabel ?? '')
    setSalesOrderLabel(draft.labels?.salesOrderLabel ?? '')
    setProjectName(draft.labels?.projectName ?? '')
    setDraftSubassemblies(draft.subassemblies)
    if (draft.header.SalesOrderDocEntry) {
      void getSalesOrder(draft.header.SalesOrderDocEntry).then((detail) => {
        setSalesOrderProducts(detail?.DocumentLines ?? [])
      })
    }
    // Restore a draft once when opening the create form; later keystrokes write back via persistDraftHeader.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isDraftParent])

  useEffect(() => {
    if (id === ROUTES.PRODUCTION_ORDER_DRAFT_ID) {
      navigate(ROUTES.PRODUCTION_ORDER_FORM, { replace: true })
    }
  }, [id, navigate])

  useEffect(() => {
    if (!id || isDraftParent) return
    getProductionOrder(id)
      .then(async (po) => {
        const applied = applyProductionCategoryDefaults(
          po.ProductionCategory ?? 'JOB',
          po,
          [],
        )
        setForm({
          ...applied.order,
          Warehouse: po.Warehouse || applied.order.Warehouse,
          IssWarehouse: po.IssWarehouse || applied.order.IssWarehouse,
        })
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
        if (po.SalesOrderDocNum) setSalesOrderLabel(String(po.SalesOrderDocNum))
        if (po.SalesOrderDocEntry) {
          const detail = await getSalesOrder(po.SalesOrderDocEntry)
          const products = detail?.DocumentLines ?? []
          setSalesOrderProducts(products)
          const cap = salesOrderPlannedQtyCap(products, po.ItemNumber)
          if (cap != null && (po.PlannedQuantity ?? 0) > cap) {
            setForm((prev) => ({ ...prev, PlannedQuantity: cap }))
          }
        }
      })
      .catch((e: unknown) => {
        const message = e instanceof Error ? e.message : 'Production order could not be loaded.'
        setError(message)
        setLoadFailed(true)
        toast.error(message)
      })
      .finally(() => setLoading(false))
  }, [id])

  const handleSalesOrderChange = async (value: string) => {
    setSalesOrderLabel(value)
    const picked = salesOrderRows.current.find((so) => String(so.DocNum ?? so.DocEntry ?? '') === value)
    const project = picked?.Project ?? ''
    const detail = picked?.DocEntry ? await getSalesOrder(picked.DocEntry) : undefined
    const products = detail?.DocumentLines ?? []
    setSalesOrderProducts(picked?.DocEntry ? products : null)
    setForm((prev) => {
      const keepItem = isItemOnSalesOrder(products, prev.ItemNumber)
      const nextItem = keepItem ? prev.ItemNumber : ''
      const cap = salesOrderPlannedQtyCap(products, nextItem)
      const planned = cap != null && (prev.PlannedQuantity ?? 0) > cap
        ? cap
        : (keepItem ? prev.PlannedQuantity : 0)
      return {
        ...prev,
        SalesOrderDocNum: picked?.DocNum ?? (value ? Number(value) : undefined),
        SalesOrderDocEntry: picked?.DocEntry,
        CustomerCode: picked?.CardCode ?? prev.CustomerCode,
        CustomerName: picked?.CardName ?? prev.CustomerName,
        Project: project,
        ItemNumber: nextItem,
        ProductDescription: keepItem ? prev.ProductDescription : undefined,
        PlannedQuantity: planned,
      }
    })
    if (!isItemOnSalesOrder(products, form.ItemNumber)) setItemLabel('')
    if (picked?.CardCode) setCustomerLabel(formatCodeWithName(picked.CardCode, picked.CardName))
    setProjectName(project ? (await resolveProject(project))?.Name ?? '' : '')
  }

  const handlePlannedQuantityChange = (value: number) => {
    const cap = salesOrderPlannedQtyCap(salesOrderProducts, form.ItemNumber)
    const next = cap != null && value > cap ? cap : value
    if (cap != null && value > cap) {
      toast.error(`Planned quantity cannot exceed ${cap} (sales order quantity × items per unit).`)
    }
    setForm((prev) => ({ ...prev, PlannedQuantity: next }))
    if (id && !lineReviewWarning) {
      toast.info('Header planned quantity changed. Review sub-assembly item quantities before saving.')
      setLineReviewWarning(true)
    }
  }

  const persistDraftHeader = useCallback(() => {
    if (!isDraftParent) return
    saveCreateDraftHeader(form, {
      customerLabel,
      itemLabel,
      salesOrderLabel,
      projectName,
    })
  }, [isDraftParent, form, customerLabel, itemLabel, salesOrderLabel, projectName])

  const persistHeader = async (after: 'list' | 'subassembly') => {
    const applied = applyProductionCategoryDefaults(form.ProductionCategory ?? 'JOB', form, [])
    const header = {
      ...applied.order,
      Warehouse: form.Warehouse || applied.order.Warehouse,
      IssWarehouse: form.IssWarehouse || applied.order.IssWarehouse,
    }
    if (isDraftParent) persistDraftHeader()
    const subassemblies = isDraftParent
      ? loadCreateDraft()?.subassemblies ?? draftSubassemblies
      : id
        ? await listSubassemblies(id)
        : []
    const validationError = validateProductionOrderForm(header, [], salesOrderProducts, {
      requireSubassemblyWithItems: after === 'list' && isDraftParent,
      subassemblies,
    })
    if (validationError) {
      setError(validationError)
      toast.error(validationError)
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload: ProductionOrder = {
        ...header,
        Type: header.Type || PRODUCTION_ORDER_TYPE_SPECIAL,
        ProjectName: projectName || header.ProjectName,
        ProductionOrderLines: undefined,
        Subassemblies: isDraftParent && after === 'list' ? subassemblies : undefined,
        PostingDate: toIsoDateOnly(header.PostingDate) ?? (isDraftParent ? todayIsoDate() : header.PostingDate),
        StartDate: toIsoDateOnly(header.StartDate) ?? todayIsoDate(),
        DueDate: toIsoDateOnly(header.DueDate) ?? todayIsoDate(),
      }
      const result = id && !isDraftParent
        ? await updateProductionOrder(Number(id), payload)
        : await createProductionOrder(payload)

      if (result?.pendingApproval) {
        const message = id && !isDraftParent
          ? 'Production order update submitted for approval. It will reach SAP after approval.'
          : 'Production order submitted for approval. It will appear in SAP after approval.'
        if (isDraftParent) clearCreateDraft()
        toast.info(message)
        navigate(ROUTES.MY_APPROVAL_REQUESTS, {
          state: { message, approvalRequestId: result.pendingApprovalRequestId },
        })
        return
      }

      const docNum = result?.DocumentNumber ?? form.DocumentNumber
      const subject = docNum ? `Production order ${docNum}` : 'Production order'
      if (isDraftParent) clearCreateDraft()
      toast.success(id && !isDraftParent ? `${subject} updated in SAP.` : `${subject} created in SAP.`)
      navigate(ROUTES.PRODUCTION_ORDERS)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Save failed'
      setError(message)
      toast.error(message)
    } finally {
      setSaving(false)
    }
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    await persistHeader('list')
  }

  const openDraftSubassembly = (childKey?: string) => {
    persistDraftHeader()
    const applied = applyProductionCategoryDefaults(form.ProductionCategory ?? 'JOB', form, [])
    const header = {
      ...applied.order,
      Warehouse: form.Warehouse || applied.order.Warehouse,
      IssWarehouse: form.IssWarehouse || applied.order.IssWarehouse,
    }
    const validationError = validateProductionOrderForm(header, [], salesOrderProducts)
    if (validationError) {
      setError(validationError)
      toast.error(validationError)
      return
    }
    navigate(productionOrderSubassemblyPath(ROUTES.PRODUCTION_ORDER_DRAFT_ID, childKey))
  }

  const handleDeleteDraft = (row: ProductionOrder) => {
    if (!row.DraftKey) return
    const next = removeDraftSubassembly(row.DraftKey)
    setDraftSubassemblies(next.subassemblies)
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
          The header planned quantity changed. Review sub-assembly item quantities before saving.
        </div>
      )}
      <form onSubmit={handleSubmit} className="space-y-6">
        <Card>
          <CardContent className="space-y-6 pt-6">
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              <Input
                label="Production Order"
                value={form.DocumentNumber != null ? String(form.DocumentNumber) : ''}
                readOnly
                placeholder="Assigned on save"
              />
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
                required={isDraftParent}
                value={String(form.SalesOrderDocNum ?? '')}
                selectedLabel={salesOrderLabel}
                placeholder="Search sales order..."
                onSearch={searchSalesOrderOptions}
                onChange={(value) => void handleSalesOrderChange(value)}
              />
              <Select label="Status" value={form.Status ?? 'boposPlanned'} onChange={(value) => setForm({ ...form, Status: value })} options={STATUS_OPTIONS} />
              <SearchableSelect
                label="Product No."
                required
                disabled={!!form.AbsoluteEntry || salesOrderProducts == null}
                value={form.ItemNumber ?? ''}
                selectedLabel={itemLabel}
                placeholder={salesOrderProducts == null ? 'Select a sales order first' : 'Search sales order item...'}
                hint={salesOrderProducts != null ? 'Only items on the selected sales order.' : undefined}
                onSearch={searchProductOptions}
                onChange={(code, option) => {
                  const label = option?.label ?? code
                  const description = nameFromCodeWithNameLabel(label, code)
                  const cap = salesOrderPlannedQtyCap(salesOrderProducts, code)
                  const planned = !form.PlannedQuantity || (cap != null && form.PlannedQuantity > cap)
                    ? (cap ?? form.PlannedQuantity)
                    : form.PlannedQuantity
                  setItemLabel(label)
                  setForm({
                    ...form,
                    ItemNumber: code,
                    ProductDescription: description,
                    PlannedQuantity: planned,
                    Type: PRODUCTION_ORDER_TYPE_SPECIAL,
                  })
                }}
              />
              <Input
                label="Product Name"
                value={form.ProductDescription ?? ''}
                readOnly
                placeholder="Taken from the selected product"
              />
              <Input
                label="Planned Qty"
                type="number"
                nonNegative
                required
                max={salesOrderPlannedQtyCap(salesOrderProducts, form.ItemNumber)}
                hint={(() => {
                  const cap = salesOrderPlannedQtyCap(salesOrderProducts, form.ItemNumber)
                  return cap != null ? `Cannot exceed ${cap} from the sales order (quantity × items per unit).` : undefined
                })()}
                value={String(form.PlannedQuantity ?? 0)}
                onChange={(e) => handlePlannedQuantityChange(Number(e.target.value))}
              />
              <Select
                label="Production Category"
                value={form.ProductionCategory ?? 'JOB'}
                onChange={(value) => {
                  const applied = applyProductionCategoryDefaults(value, form, [])
                  setForm(applied.order)
                }}
                options={CATEGORY_OPTIONS}
              />
              <Input
                label="Project Code"
                value={form.Project ?? ''}
                readOnly
                hint="Taken from the selected sales order."
                placeholder="Select a sales order"
              />
              <Input label="Project Name" value={projectName} readOnly placeholder="Select a sales order" />
              <SapDateInput
                label="Order Date"
                value={form.CreationDate ?? form.PostingDate}
                onChangeIso={(date) => setForm({ ...form, CreationDate: date })}
                disabled={!!form.AbsoluteEntry}
              />
              <SapDateInput
                label="Start Date"
                value={form.StartDate}
                onChangeIso={(date) => setForm({ ...form, StartDate: date })}
              />
              <SapDateInput
                label="Due Date"
                required
                value={form.DueDate}
                onChangeIso={(date) => setForm({ ...form, DueDate: date })}
              />
            </div>
          </CardContent>
        </Card>

        <Tabs value="subassemblies" onValueChange={() => undefined}>
          <TabsList aria-label="Production order sections">
            <TabsTrigger value="subassemblies" icon={<Layers className="h-4 w-4" />}>
              Sub-assemblies
            </TabsTrigger>
          </TabsList>
          <TabsContent value="subassemblies">
            <ProductionOrderSubassembliesCard
              parent={form}
              draftRows={isDraftParent ? draftSubassemblies : undefined}
              adding={saving}
              onAddUnsaved={() => openDraftSubassembly()}
              onEditDraft={(row) => openDraftSubassembly(row.DraftKey)}
              onDeleteDraft={handleDeleteDraft}
            />
          </TabsContent>
        </Tabs>

        <Card>
          <CardContent className="space-y-6 pt-6">
            <Textarea
              label="Remarks"
              value={form.Remarks ?? ''}
              onChange={(e) => setForm({ ...form, Remarks: e.target.value })}
              placeholder="Remarks for this production order"
            />

            <div className="flex gap-3">
              <Button type="submit" isLoading={saving}>{form.AbsoluteEntry ? 'Update' : 'Add'}</Button>
              <Button type="button" variant="outline" onClick={() => {
                if (isDraftParent) clearCreateDraft()
                navigate(ROUTES.PRODUCTION_ORDERS)
              }}>Cancel</Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  )
}
