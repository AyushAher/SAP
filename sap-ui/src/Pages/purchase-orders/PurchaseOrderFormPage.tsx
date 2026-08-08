import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Banknote, ClipboardList, Truck } from 'lucide-react'
import { PurchaseOrderLinesEditor } from '@/Components/forms/PurchaseOrderLinesEditor'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PreviousNextButtons } from '@/Components/shared/PreviousNextButtons'
import {
  BlockingLoader,
  Button,
  Card,
  CardContent,
  Input,
  SearchableSelect,
  Select,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
  Textarea,
} from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { formatCodeWithName, resolveMasterSelectLabels } from '@/helpers/masterLookup'
import {
  applyLogisticsToPo,
  applyOtherTermsToPo,
  applyPaymentPercentToTerm,
  applyPaymentTermsToPo,
  calculatePurchaseOrderTotals,
  formatPoAmount,
  nextPaymentTermSlot,
  parsePaymentTermsFromPo,
  paymentTermDisplayLabel,
  readLogisticsFromPo,
  readOtherTermsFromPo,
  resolvePaymentTermPercent,
} from '@/helpers/purchaseOrderForm'
import { useAppSelector } from '@/store/hooks'
import { getBranchesApi } from '@/Requests/auth'
import {
  fetchPaymentTermTypes,
  searchBusinessPartners,
  searchEmployees,
  searchProjects,
  searchSalesPersons,
  searchVendors,
  searchWarehouses,
  formatWarehouseOptionLabel,
  lookupBusinessPartner,
  lookupEmployee,
  lookupSalesPerson,
  type MasterBusinessPartner,
  type PaymentTermTypeOption,
} from '@/Requests/masters'
import { createPurchaseOrder, updatePurchaseOrder, type PurchaseOrder } from '@/Requests/purchaseOrders'
import {
  useInvalidatePurchaseOrders,
  usePurchaseOrder,
} from '@/hooks/usePurchaseOrders'
import { toast } from '@/helpers/toast'
import {
  PO_DOC_TYPE,
  PO_DOC_TYPE_OPTIONS,
  PO_TN,
  PO_TYPE_OPTIONS,
  isServicePoDocType,
  validatePurchaseOrderAgainstTn,
} from '@/helpers/purchaseOrderTnValidation'
import type { SelectOption } from '@/types'
import type {
  PaymentTermRow,
  PurchaseOrderLineItem,
  PurchaseOrderLogistics,
  PurchaseOrderOtherTerms,
} from '@/types/purchaseOrder'
import {
  PAYMENT_TERM_TYPE_OPTIONS,
  PRICE_BASIS_OPTIONS,
  MODE_OF_TRANSPORT_OPTIONS,
} from '@/types/purchaseOrder'
import { useQuery } from '@tanstack/react-query'

type FormTab = 'logistics' | 'payment' | 'other'

const FORM_TABS: Array<{ id: FormTab; label: string; description: string }> = [
  { id: 'logistics', label: 'Logistics', description: 'Dispatch, shipping, and transport details.' },
  { id: 'payment', label: 'Payment Terms', description: 'Define stage-wise payment terms for this order.' },
  { id: 'other', label: 'Other Terms', description: 'Commercial terms, warranty, and additional conditions.' },
]

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

type PaymentTermDraft = Omit<PaymentTermRow, 'id'>

function emptyPaymentTermDraft(): PaymentTermDraft {
  return { type: '', basic: undefined, gst: undefined, stage: '', desc: '' }
}

function paymentTermTypeOptionsFromApi(options: PaymentTermTypeOption[] | undefined): SelectOption[] {
  const source = options?.length
    ? options
    : PAYMENT_TERM_TYPE_OPTIONS.map((o) => ({ value: o.value, description: o.label }))
  return source.map((o) => ({ value: o.value, label: o.description || o.value }))
}

export function PurchaseOrderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const authBranchId = useAppSelector((state) => state.auth.branchId)
  const invalidatePurchaseOrders = useInvalidatePurchaseOrders()
  const {
    data: purchaseOrder,
    isLoading: queryLoading,
    error: queryError,
  } = usePurchaseOrder(id)

  const { data: paymentTermTypeOptions } = useQuery({
    queryKey: ['masters', 'payment-term-types'],
    queryFn: fetchPaymentTermTypes,
    staleTime: 20 * 60 * 1000,
  })

  const paymentTypeSelectOptions = useMemo(
    () => paymentTermTypeOptionsFromApi(paymentTermTypeOptions),
    [paymentTermTypeOptions],
  )

  const paymentTypeLabelMap = useMemo(() => {
    const map: Record<string, string> = {}
    for (const opt of paymentTypeSelectOptions) map[opt.value] = opt.label
    return map
  }, [paymentTypeSelectOptions])

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hydratedId, setHydratedId] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<FormTab>('logistics')
  const [form, setForm] = useState<Record<string, unknown>>({
    CardCode: '',
    CardName: '',
    Project: '',
    Comments: '',
    NumAtCard: '',
    DocType: PO_DOC_TYPE.items,
    SalesPersonCode: undefined,
    DocumentsOwner: undefined,
    U_PO_Type: '',
    U_TRN: '',
    U_DisID: '',
    U_Owner: '',
    U_Stage: '',
    U_Warehouse: '',
    DocDate: todayIsoDate(),
    PostingDate: todayIsoDate(),
    TaxDate: todayIsoDate(),
    DocDueDate: '',
    DueDate: '',
    BPLId: authBranchId ?? 1,
    RoundingDiffAmount: 0,
    DocumentLines: [],
  })
  const [lines, setLines] = useState<PurchaseOrderLineItem[]>([])
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermRow[]>([])
  const [paymentDraft, setPaymentDraft] = useState(emptyPaymentTermDraft())
  const [logistics, setLogistics] = useState<PurchaseOrderLogistics>({})
  const [otherTerms, setOtherTerms] = useState<PurchaseOrderOtherTerms>({})

  const [vendorLabel, setVendorLabel] = useState('')
  const [vendorSeries, setVendorSeries] = useState<number | null>(null)
  const [projectLabel, setProjectLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [buyerLabel, setBuyerLabel] = useState('')
  const [approverLabel, setApproverLabel] = useState('')
  const [dispatchToLabel, setDispatchToLabel] = useState('')
  const [branchOptions, setBranchOptions] = useState<SelectOption[]>([])

  const loading = Boolean(id) && (queryLoading || hydratedId !== String(id))
  const loadError = error
    ?? (queryError instanceof Error ? queryError.message : queryError ? 'Failed to load purchase order' : null)

  const defaultWarehouse = String(form.U_Warehouse ?? '')
  const docType = String(form.DocType ?? PO_DOC_TYPE.items)
  const isServiceDoc = isServicePoDocType(docType)
  const isJobPo = String(form.U_PO_Type ?? '').trim().toUpperCase() === PO_TN.jobPoType
  const isTransporterVendor = !isServiceDoc && vendorSeries === PO_TN.transporterBpSeries
  const usesDrpWarehouse = !isServiceDoc && lines.some((line) => {
    const wh = (line.WarehouseCode ?? '').trim().toUpperCase()
    return wh === 'DRP' || wh === 'DRP2'
  })

  const totals = useMemo(
    () => calculatePurchaseOrderTotals(lines, Number(form.RoundingDiffAmount ?? 0)),
    [lines, form.RoundingDiffAmount],
  )

  const searchVendorOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchVendors(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
      meta: v,
    })).filter((o) => o.value)
  }, [])

  const searchBusinessPartnerOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchBusinessPartners(search)
    return (response.data ?? []).map((bp) => ({
      value: bp.CardCode ?? '',
      label: `${bp.CardCode ?? ''} - ${bp.CardName ?? ''}`.trim(),
      meta: bp,
    })).filter((o) => o.value)
  }, [])

  const searchBuyerOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchSalesPersons(search)
    return (response.data ?? [])
      .filter((sp) => sp.SalesEmployeeCode != null && sp.SalesEmployeeCode !== PO_TN.noBuyerCode)
      .map((sp) => ({
        value: String(sp.SalesEmployeeCode),
        label: `${sp.SalesEmployeeCode} - ${sp.SalesEmployeeName ?? ''}`.trim(),
      }))
  }, [])

  const searchApproverOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchEmployees(search)
    return (response.data ?? []).map((emp) => ({
      value: String(emp.EmployeeID),
      label: emp.DisplayName ?? String(emp.EmployeeID),
    })).filter((o) => o.value)
  }, [])

  const searchProjectOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchProjects(search)
    return (response.data ?? []).map((p) => ({
      value: p.Code ?? '',
      label: `${p.Code ?? ''} - ${p.Name ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [])

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: formatWarehouseOptionLabel(wh),
    })).filter((o) => o.value)
  }, [])

  useEffect(() => {
    getBranchesApi()
      .then((items) => setBranchOptions(items.map((b) => ({ value: String(b.id), label: b.name }))))
      .catch(() => setBranchOptions([]))
  }, [])

  useEffect(() => {
    if (!id) {
      if (authBranchId) setForm((prev) => ({ ...prev, BPLId: authBranchId }))
      setHydratedId(null)
      return
    }
    if (!purchaseOrder || queryLoading)
      return

    let cancelled = false
    void (async () => {
      const record = purchaseOrder as Record<string, unknown>
      setForm({
        ...record,
        DocType: record.DocType || PO_DOC_TYPE.items,
      })
      setLines(((purchaseOrder.DocumentLines as PurchaseOrderLineItem[] | undefined) ?? []).map((line) => {
        const purchaseQty = line.Quantity ?? 0
        const unitsPer = line.UnitsOfMeasurment
        const stockQty = line.StockQty
          ?? (line as { InventoryQuantity?: number }).InventoryQuantity
          ?? (purchaseQty > 0 && unitsPer != null ? purchaseQty * unitsPer : undefined)
        return {
          ...line,
          StockQty: stockQty,
          UnitsOfMeasurment: unitsPer ?? (stockQty != null && purchaseQty > 0 ? stockQty / purchaseQty : undefined),
          StockUom: line.StockUom,
          UoMCode: line.UoMCode ?? line.UomName,
        }
      }))
      setPaymentTerms(parsePaymentTermsFromPo(record))
      const loadedLogistics = readLogisticsFromPo(record)
      setLogistics(loadedLogistics)
      setOtherTerms(readOtherTermsFromPo(record))
      setDispatchToLabel('')
      const buyerCode = record.SalesPersonCode != null ? Number(record.SalesPersonCode) : null
      const approverId = record.DocumentsOwner != null ? Number(record.DocumentsOwner) : null
      // Do not set raw codes as labels — wait for master lookups so dropdowns show names.
      setBuyerLabel('')
      setApproverLabel('')
      try {
        const [labels, buyer, approver, vendorMatch, dispatchBp] = await Promise.all([
          resolveMasterSelectLabels({
            vendorCode: purchaseOrder.CardCode,
            projectCode: purchaseOrder.Project,
          }),
          buyerCode != null && Number.isFinite(buyerCode) ? lookupSalesPerson(buyerCode) : Promise.resolve(undefined),
          approverId != null && Number.isFinite(approverId) ? lookupEmployee(approverId) : Promise.resolve(undefined),
          purchaseOrder.CardCode
            ? searchVendors(purchaseOrder.CardCode, 5).then((res) =>
              (res.data ?? []).find((v) => v.CardCode === purchaseOrder.CardCode))
            : Promise.resolve(undefined),
          loadedLogistics.dispatchTo
            ? lookupBusinessPartner(loadedLogistics.dispatchTo)
            : Promise.resolve(undefined),
        ])
        if (cancelled) return
        if (purchaseOrder.CardCode) {
          setVendorLabel(labels.vendorLabel ?? formatCodeWithName(purchaseOrder.CardCode, purchaseOrder.CardName))
          setVendorSeries(vendorMatch?.Series ?? null)
        } else {
          setVendorSeries(null)
        }
        if (purchaseOrder.Project) {
          setProjectLabel(labels.projectLabel ?? formatCodeWithName(purchaseOrder.Project))
        }
        if (buyer) {
          setBuyerLabel(`${buyer.SalesEmployeeCode} - ${buyer.SalesEmployeeName ?? ''}`.trim())
        } else if (buyerCode != null && Number.isFinite(buyerCode)) {
          setBuyerLabel(String(buyerCode))
        }
        if (approver?.DisplayName) {
          setApproverLabel(`${approver.EmployeeID} - ${approver.DisplayName}`.trim())
        } else if (approverId != null && Number.isFinite(approverId)) {
          setApproverLabel(String(approverId))
        }
        if (loadedLogistics.dispatchTo) {
          setDispatchToLabel(
            dispatchBp
              ? formatCodeWithName(dispatchBp.CardCode, dispatchBp.CardName)
              : formatCodeWithName(loadedLogistics.dispatchTo),
          )
        }
      } catch {
        // labels are optional enrichments
        if (buyerCode != null && Number.isFinite(buyerCode)) setBuyerLabel(String(buyerCode))
        if (approverId != null && Number.isFinite(approverId)) setApproverLabel(String(approverId))
      }
      if (cancelled) return
      const wh = String(record.U_Warehouse ?? '')
      if (wh) setWarehouseLabel(wh)
      setHydratedId(String(id))
    })()

    return () => {
      cancelled = true
    }
  }, [id, purchaseOrder, queryLoading, authBranchId])

  const handleAddPaymentTerm = () => {
    const slot = nextPaymentTermSlot(paymentTerms)
    if (slot == null) {
      setError('Maximum payment terms reached.')
      return
    }
    const percent = resolvePaymentTermPercent(paymentDraft)
    if (!paymentDraft.type && percent == null && !paymentDraft.stage) {
      setError('Enter at least type, percentage, or stage for the payment term.')
      return
    }
    const mapped = applyPaymentPercentToTerm(
      {
        type: paymentDraft.type || undefined,
        basic: undefined,
        gst: undefined,
        stage: paymentDraft.stage || undefined,
        desc: paymentDraft.desc || undefined,
      },
      percent,
      paymentDraft.type,
    )
    setPaymentTerms([
      ...paymentTerms,
      {
        id: slot,
        ...mapped,
      },
    ])
    setPaymentDraft(emptyPaymentTermDraft())
    setError(null)
  }

  const handleRemovePaymentTerm = (termId: number) => {
    setPaymentTerms(paymentTerms.filter((term) => term.id !== termId))
  }

  const buildPayload = (): PurchaseOrder => {
    const docDate = String(form.DocDate ?? form.PostingDate ?? todayIsoDate()).slice(0, 10)
    const docDue = String(form.DocDueDate ?? form.DueDate ?? '').slice(0, 10)
    // SAP Document Date (TaxDate) always matches Posting Date (DocDate).
    const taxDate = docDate
    let payload: Record<string, unknown> = {
      ...form,
      DocumentLines: lines.map((line) => (
        isServiceDoc
          ? {
              ItemDescription: line.ItemDescription,
              AccountCode: line.AccountCode,
              Quantity: line.Quantity,
              UnitPrice: line.UnitPrice,
              DiscountPercent: line.DiscountPercent,
              TaxCode: line.TaxCode,
              SACEntry: line.SACEntry,
              ProjectCode: line.ProjectCode || form.Project || undefined,
              CostingCode: line.CostingCode,
              U_ProdNo: line.U_ProdNo || undefined,
              LineNum: line.LineNum,
            }
          : {
              ItemCode: line.ItemCode,
              ItemDescription: line.ItemDescription,
              Quantity: line.Quantity,
              UnitPrice: line.UnitPrice,
              DiscountPercent: line.DiscountPercent,
              WarehouseCode: line.WarehouseCode,
              TaxCode: line.TaxCode,
              HSNEntry: line.HSNEntry,
              SACEntry: line.SACEntry,
              UoMCode: line.UoMCode ?? line.UomName,
              UnitsOfMeasurment: line.UnitsOfMeasurment
                ?? (line.StockQty != null && line.Quantity != null && line.Quantity > 0
                  ? line.StockQty / line.Quantity
                  : undefined),
              InventoryQuantity: line.StockQty,
              UseBaseUnits: line.UseBaseUnits
                ?? (() => {
                  const factor = line.UnitsOfMeasurment
                    ?? (line.StockQty != null && line.Quantity != null && line.Quantity > 0
                      ? line.StockQty / line.Quantity
                      : undefined)
                  if (factor == null || !Number.isFinite(factor)) return undefined
                  return Math.abs(factor - 1) < 1e-9 ? 'tYES' : 'tNO'
                })(),
              ProjectCode: line.ProjectCode || form.Project || undefined,
              CostingCode: line.CostingCode,
              U_ProdNo: line.U_ProdNo || undefined,
              LineNum: line.LineNum,
            }
      )),
      DocType: docType,
      DocDate: docDate,
      DocDueDate: docDue,
      TaxDate: taxDate,
      BPL_IDAssignedToInvoice: form.BPLId ?? authBranchId ?? 1,
      BPLId: form.BPLId ?? authBranchId ?? 1,
      SalesPersonCode: form.SalesPersonCode != null ? Number(form.SalesPersonCode) : undefined,
      DocumentsOwner: form.DocumentsOwner != null ? Number(form.DocumentsOwner) : undefined,
      U_PO_Type: form.U_PO_Type || undefined,
      U_TRN: form.U_TRN || undefined,
      U_DisID: form.U_DisID || undefined,
      NumAtCard: form.NumAtCard || undefined,
      Comments: form.Comments,
      U_Owner: form.U_Owner,
      U_Stage: form.U_Stage,
      U_Warehouse: form.U_Warehouse,
      RoundingDiffAmount: totals.roundingOff,
    }
    // Do not send client-calculated totals — SAP computes them.
    delete payload.DocTotal
    delete payload.VatSum
    delete payload.PostingDate
    delete payload.DueDate
    payload = applyPaymentTermsToPo(payload, paymentTerms)
    payload = applyLogisticsToPo(payload, logistics)
    payload = applyOtherTermsToPo(payload, otherTerms)
    return payload as PurchaseOrder
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!lines.length) {
      setError(isServiceDoc ? 'Add at least one service line.' : 'Add at least one line item.')
      toast.error(isServiceDoc ? 'Add at least one service line.' : 'Add at least one line item.')
      return
    }
    if (!form.CardCode) {
      setError('Select a business partner.')
      toast.error('Select a business partner.')
      return
    }
    const deliveryDate = String(form.DocDueDate ?? form.DueDate ?? '').trim()
    if (!deliveryDate) {
      setError('Delivery Date is required.')
      toast.error('Delivery Date is required.')
      return
    }
    const tnError = validatePurchaseOrderAgainstTn({
      salesPersonCode: form.SalesPersonCode != null ? Number(form.SalesPersonCode) : null,
      documentsOwner: form.DocumentsOwner != null ? Number(form.DocumentsOwner) : null,
      poType: String(form.U_PO_Type ?? ''),
      docType,
      trn: String(form.U_TRN ?? ''),
      disId: String(form.U_DisID ?? ''),
      dispachAdd: String(logistics.dispatchAddress ?? ''),
      vendorSeries,
      lines,
    })
    if (tnError) {
      setError(tnError)
      toast.error(tnError)
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload = buildPayload()
      const result = id
        ? await updatePurchaseOrder(Number(id), payload)
        : await createPurchaseOrder(payload)

      // Above-threshold POs are stored as approval requests until approved — not created in SAP yet.
      if (result?.pendingApproval) {
        toast.info(
          id
            ? 'Purchase order update submitted for approval.'
            : 'Purchase order submitted for approval. It will appear in SAP after approval.',
        )
        await invalidatePurchaseOrders(id)
        navigate(ROUTES.MY_APPROVAL_REQUESTS, {
          state: {
            message: id
              ? 'Purchase order update submitted for approval. It will sync to SAP after approval.'
              : 'Purchase order submitted for approval. It will appear in SAP after approval.',
            approvalRequestId: result.pendingApprovalRequestId,
          },
        })
        return
      }

      // Never redirect on create unless SAP returned a DocNum.
      const sapError =
        typeof result?.error === 'object' && result.error !== null
          ? ((result.error as { message?: { value?: string } }).message?.value
            ?? 'SAP rejected the purchase order.')
          : null
      if (sapError) {
        setError(sapError)
        toast.error(sapError)
        return
      }
      if (!id && result?.DocNum == null) {
        const message = 'Purchase order was not created in SAP (missing document number).'
        setError(message)
        toast.error(message)
        return
      }

      toast.success(
        result?.DocNum != null
          ? `Purchase order ${result.DocNum} saved.`
          : 'Purchase order saved.',
      )
      await invalidatePurchaseOrders(id)
      navigate(ROUTES.PURCHASE_ORDERS)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Save failed'
      setError(message)
      toast.error(message)
    } finally {
      setSaving(false)
    }
  }

  const updateForm = (patch: Record<string, unknown>) => setForm((prev) => ({ ...prev, ...patch }))

  return (
    <div className="min-w-0 space-y-6">
      <PageHeader title={id ? 'Edit Purchase Order' : 'New Purchase Order'} />
      <BlockingLoader
        visible={loading || saving}
        label={loading ? 'Loading purchase order...' : 'Saving purchase order...'}
        lockScroll={false}
      />
      {loadError && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{loadError}</div>}

      <Card>
        <CardContent className="space-y-6 pt-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Header</h3>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <SearchableSelect
                  label="BP Name"
                  lookupKind="businessPartner"
                  required
                  disabled={!!id}
                  value={String(form.CardCode ?? '')}
                  selectedLabel={vendorLabel}
                  placeholder="Search vendor..."
                  onSearch={searchVendorOptions}
                  onChange={(cardCode, option) => {
                    const label = option?.label ?? cardCode
                    const cardName = label.includes(' - ') ? label.split(' - ').slice(1).join(' - ') : ''
                    const meta = option?.meta as MasterBusinessPartner | undefined
                    setVendorLabel(label)
                    setVendorSeries(meta?.Series ?? null)
                    updateForm({ CardCode: cardCode, CardName: cardName })
                  }}
                />
                <Select
                  label="Type"
                  options={PO_DOC_TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                  value={docType}
                  disabled={!!id}
                  onChange={(value) => {
                    const next = value || PO_DOC_TYPE.items
                    if (next === docType) return
                    if (lines.length > 0) {
                      const ok = window.confirm(
                        'Switching between Item and Service clears existing lines. Continue?',
                      )
                      if (!ok) return
                      setLines([])
                    }
                    updateForm({ DocType: next })
                  }}
                  placeholder="Select type"
                />
                <Select
                  label="PO Type"
                  options={PO_TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                  value={String(form.U_PO_Type ?? '')}
                  onChange={(value) => updateForm({ U_PO_Type: value })}
                  placeholder="Select PO type"
                  hint={isJobPo ? 'JOB: Production Order No is required on every line.' : undefined}
                />
                <Input
                  label="Posting Date"
                  type="date"
                  value={String(form.DocDate ?? form.PostingDate ?? '').slice(0, 10)}
                  onChange={(e) => {
                    const date = e.target.value
                    updateForm({ DocDate: date, PostingDate: date, TaxDate: date })
                  }}
                  hint="Document Date is set to the same value."
                />
                <SearchableSelect
                  label="Project"
                  lookupKind="project"
                  value={String(form.Project ?? '')}
                  selectedLabel={projectLabel}
                  placeholder="Search project..."
                  onSearch={searchProjectOptions}
                  onChange={(projectCode, option) => {
                    setProjectLabel(option?.label ?? projectCode)
                    updateForm({ Project: projectCode })
                  }}
                />
                <Input
                  label="Delivery Date"
                  type="date"
                  required
                  value={String(form.DocDueDate ?? form.DueDate ?? '').slice(0, 10)}
                  onChange={(e) => updateForm({ DocDueDate: e.target.value, DueDate: e.target.value })}
                />
                <Input
                  label="Vendor Ref."
                  value={String(form.NumAtCard ?? '')}
                  onChange={(e) => updateForm({ NumAtCard: e.target.value })}
                />
                <SearchableSelect
                  label="Warehouse"
                  value={String(form.U_Warehouse ?? '')}
                  selectedLabel={warehouseLabel}
                  placeholder="Search warehouse..."
                  onSearch={searchWarehouseOptions}
                  onChange={(code, option) => {
                    setWarehouseLabel(option?.label ?? code)
                    updateForm({ U_Warehouse: code })
                  }}
                />
                <Select
                  label="Branch"
                  options={branchOptions}
                  value={String(form.BPLId ?? authBranchId ?? '')}
                  onChange={(value) => updateForm({ BPLId: Number(value) })}
                  placeholder="Select branch"
                />
                {usesDrpWarehouse ? (
                  <Input
                    label="Dispatch ID (U_DisID) *"
                    value={String(form.U_DisID ?? '')}
                    onChange={(e) => updateForm({ U_DisID: e.target.value })}
                    hint="Required when any line uses DRP / DRP2 warehouse. Dispatch address is on the Logistics tab."
                  />
                ) : null}
                {isTransporterVendor ? (
                  <p className="md:col-span-2 xl:col-span-4 text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
                    Transporter vendor (series 124): add item <strong>{PO_TN.transporterMandatoryItem}</strong>.
                  </p>
                ) : null}
              </div>

              <div className="space-y-3 border-t border-slate-200 pt-4">
                <h4 className="text-sm font-semibold text-slate-700">
                  {isServiceDoc ? 'Service Details' : 'Item Details'}
                </h4>
                <p className="text-sm text-slate-500">
                  {isServiceDoc
                    ? 'Add and manage G/L service lines for this purchase order.'
                    : 'Add and manage purchase order line items.'}
                </p>
                <PurchaseOrderLinesEditor
                  lines={lines}
                  onChange={setLines}
                  defaultWarehouse={defaultWarehouse}
                  defaultProject={String(form.Project ?? '')}
                  docType={docType}
                  requireProdNo={isJobPo}
                />
              </div>
            </section>

            <section>
              <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as FormTab)}>
                <TabsList aria-label="Purchase order sections" className="-mx-1 px-1">
                  <TabsTrigger value="logistics" icon={<Truck className="h-4 w-4" />}>
                    Logistics
                  </TabsTrigger>
                  <TabsTrigger value="payment" icon={<Banknote className="h-4 w-4" />} badge={paymentTerms.length}>
                    Payment Terms
                  </TabsTrigger>
                  <TabsTrigger value="other" icon={<ClipboardList className="h-4 w-4" />}>
                    Other Terms
                  </TabsTrigger>
                </TabsList>

                <TabsContent
                  value="logistics"
                  title={FORM_TABS[0].label}
                  description={FORM_TABS[0].description}
                >
                  <div className="grid gap-4 md:grid-cols-2">
                  <SearchableSelect
                    label="Dispatch To / Ship To"
                    lookupKind="businessPartner"
                    value={logistics.dispatchTo ?? ''}
                    selectedLabel={dispatchToLabel}
                    placeholder="Search business partner..."
                    onSearch={searchBusinessPartnerOptions}
                    onChange={(cardCode, option) => {
                      setDispatchToLabel(option?.label ?? cardCode)
                      setLogistics({ ...logistics, dispatchTo: cardCode || undefined })
                    }}
                  />
                  <Input
                    label="Dispatch Address"
                    value={logistics.dispatchAddress ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, dispatchAddress: e.target.value })}
                    hint={usesDrpWarehouse ? 'Required when any line uses DRP / DRP2 warehouse.' : undefined}
                  />
                  <Input
                    label="Contact Person"
                    value={logistics.contactPerson ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, contactPerson: e.target.value })}
                  />
                  <Select
                    label="Price Basis"
                    options={PRICE_BASIS_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                    value={logistics.priceBasis ?? ''}
                    onChange={(value) => setLogistics({ ...logistics, priceBasis: value || undefined })}
                    placeholder="Select price basis"
                  />
                  <Select
                    label="Mode of Transport"
                    options={MODE_OF_TRANSPORT_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                    value={logistics.modeOfTransport ?? ''}
                    onChange={(value) => setLogistics({ ...logistics, modeOfTransport: value || undefined })}
                    placeholder="Select mode of transport"
                  />
                </div>
                </TabsContent>

                <TabsContent
                  value="payment"
                  title={FORM_TABS[1].label}
                  description={FORM_TABS[1].description}
                >
                <div className="space-y-4">
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                    <Select
                      label="Type"
                      options={paymentTypeSelectOptions}
                      value={paymentDraft.type ?? ''}
                      onChange={(value) => {
                        const percent = resolvePaymentTermPercent(paymentDraft)
                        setPaymentDraft(applyPaymentPercentToTerm(paymentDraft, percent, value))
                      }}
                      placeholder="Select type"
                    />
                    <Input
                      label="Payment %"
                      type="number"
                      min="0"
                      nonNegative
                      value={resolvePaymentTermPercent(paymentDraft) != null ? String(resolvePaymentTermPercent(paymentDraft)) : ''}
                      onChange={(e) => {
                        const percent = e.target.value === '' ? undefined : Number(e.target.value)
                        setPaymentDraft(applyPaymentPercentToTerm(paymentDraft, percent, paymentDraft.type))
                      }}
                    />
                    <Input
                      label="Stage"
                      value={paymentDraft.stage ?? ''}
                      onChange={(e) => setPaymentDraft({ ...paymentDraft, stage: e.target.value })}
                    />
                    <div className="flex items-end">
                      <Button type="button" onClick={handleAddPaymentTerm}>Add</Button>
                    </div>
                  </div>
                  <Input
                    label="Description"
                    value={paymentDraft.desc ?? ''}
                    onChange={(e) => setPaymentDraft({ ...paymentDraft, desc: e.target.value })}
                  />

                  <div className="overflow-x-auto rounded-lg border border-slate-200">
                    <table className="min-w-full text-sm">
                      <thead className="bg-slate-50 text-left text-slate-600">
                        <tr>
                          <th className="px-3 py-2 font-medium">#</th>
                          <th className="px-3 py-2 font-medium">Type</th>
                          <th className="px-3 py-2 font-medium">Payment %</th>
                          <th className="px-3 py-2 font-medium">Stage</th>
                          <th className="px-3 py-2 font-medium">Description</th>
                          <th className="px-3 py-2 font-medium">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {paymentTerms.length === 0 ? (
                          <tr>
                            <td colSpan={6} className="px-3 py-6 text-center text-slate-500">No payment terms added.</td>
                          </tr>
                        ) : paymentTerms.map((term) => (
                          <tr key={term.id} className="border-t border-slate-100">
                            <td className="px-3 py-2">{term.id}</td>
                            <td className="px-3 py-2">{paymentTypeLabelMap[term.type ?? ''] ?? term.type ?? '—'}</td>
                            <td className="px-3 py-2">{resolvePaymentTermPercent(term) ?? '—'}</td>
                            <td className="px-3 py-2">{term.stage ?? '—'}</td>
                            <td className="px-3 py-2">{paymentTermDisplayLabel(term, paymentTypeLabelMap)}</td>
                            <td className="px-3 py-2">
                              <Button type="button" variant="outline" size="sm" onClick={() => handleRemovePaymentTerm(term.id)}>
                                Remove
                              </Button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
                </TabsContent>

                <TabsContent
                  value="other"
                  title={FORM_TABS[2].label}
                  description={FORM_TABS[2].description}
                >
                <div className="grid gap-4 md:grid-cols-2">
                  <Input label="Delivery Terms" value={otherTerms.deliveryTerms ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, deliveryTerms: e.target.value })} />
                  <Input label="Inspection By" value={otherTerms.inspectionBy ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, inspectionBy: e.target.value })} />
                  <Input label="Transportation" value={otherTerms.transportation ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, transportation: e.target.value })} />
                  <Input label="Supervision" value={otherTerms.supervision ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, supervision: e.target.value })} />
                  <Input label="Transit Insurance" value={otherTerms.transitInsurance ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, transitInsurance: e.target.value })} />
                  <Input label="Drawing & Documents" value={otherTerms.drawingDocuments ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, drawingDocuments: e.target.value })} />
                  <Input label="Loading" value={otherTerms.loading ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, loading: e.target.value })} />
                  <Input label="Warranty" value={otherTerms.warranty ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, warranty: e.target.value })} />
                  <Input label="Unloading" value={otherTerms.unloading ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, unloading: e.target.value })} />
                  <Input label="Any Other Remark" value={otherTerms.otherRemark ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, otherRemark: e.target.value })} />
                  <Input label="Painting" value={otherTerms.painting ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, painting: e.target.value })} />
                  <Input label="Test Certificates" value={otherTerms.testCertificates ?? ''} onChange={(e) => setOtherTerms({ ...otherTerms, testCertificates: e.target.value })} />
                </div>
                </TabsContent>
              </Tabs>
            </section>

            <section className="grid gap-4 border-t border-slate-200 pt-4 md:grid-cols-2">
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <SearchableSelect
                    label="Buyer *"
                    value={form.SalesPersonCode != null ? String(form.SalesPersonCode) : ''}
                    selectedLabel={buyerLabel}
                    placeholder="Search buyer..."
                    onSearch={searchBuyerOptions}
                    onChange={(value, option) => {
                      const code = value ? Number(value) : undefined
                      setBuyerLabel(option?.label ?? value)
                      updateForm({ SalesPersonCode: Number.isFinite(code) ? code : undefined })
                    }}
                  />
                  <SearchableSelect
                    label="Approver *"
                    value={form.DocumentsOwner != null ? String(form.DocumentsOwner) : ''}
                    selectedLabel={approverLabel}
                    placeholder="Search approver..."
                    onSearch={searchApproverOptions}
                    onChange={(value, option) => {
                      const empId = value ? Number(value) : undefined
                      setApproverLabel(option?.label ?? value)
                      updateForm({ DocumentsOwner: Number.isFinite(empId) ? empId : undefined })
                    }}
                  />
                </div>
                <Textarea
                  label="User Remarks"
                  value={String(form.Comments ?? '')}
                  onChange={(e) => updateForm({ Comments: e.target.value })}
                />
              </div>
              <div className="space-y-3 rounded-lg bg-slate-50 p-4">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-slate-600">Total Before Discount</span>
                  <span className="font-semibold text-slate-900">{formatPoAmount(totals.totalBeforeDiscount)}</span>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-slate-600">Tax</span>
                  <span className="font-semibold text-slate-900">{formatPoAmount(totals.tax)}</span>
                </div>
                <Input
                  label="Rounding Off"
                  type="number"
                  step="0.01"
                  value={String(form.RoundingDiffAmount ?? 0)}
                  onChange={(e) => updateForm({ RoundingDiffAmount: Number(e.target.value) })}
                />
                <div className="flex items-center justify-between border-t border-slate-200 pt-3 text-base">
                  <span className="font-medium text-slate-700">Total Payment Due</span>
                  <span className="text-lg font-bold text-primary-700">{formatPoAmount(totals.totalPaymentDue)}</span>
                </div>
              </div>
            </section>

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
