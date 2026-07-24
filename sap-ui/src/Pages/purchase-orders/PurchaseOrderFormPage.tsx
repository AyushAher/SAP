import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Banknote, ClipboardList, Package, Truck } from 'lucide-react'
import { PurchaseOrderLinesEditor } from '@/Components/forms/PurchaseOrderLinesEditor'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PreviousNextButtons } from '@/Components/shared/PreviousNextButtons'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
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
  applyPaymentTermsToPo,
  calculatePurchaseOrderTotals,
  formatPoAmount,
  nextPaymentTermSlot,
  parsePaymentTermsFromPo,
  paymentTermDisplayLabel,
  readLogisticsFromPo,
  readOtherTermsFromPo,
} from '@/helpers/purchaseOrderForm'
import { useAppSelector } from '@/store/hooks'
import { getBranchesApi } from '@/Requests/auth'
import { searchProjects, searchVendors, searchWarehouses } from '@/Requests/masters'
import { createPurchaseOrder, updatePurchaseOrder, type PurchaseOrder } from '@/Requests/purchaseOrders'
import {
  useInvalidatePurchaseOrders,
  usePurchaseOrder,
} from '@/hooks/usePurchaseOrders'
import type { SelectOption } from '@/types'
import type { PaymentTermRow, PurchaseOrderLineItem, PurchaseOrderLogistics, PurchaseOrderOtherTerms } from '@/types/purchaseOrder'
import { PAYMENT_TERM_TYPE_OPTIONS } from '@/types/purchaseOrder'

type FormTab = 'items' | 'logistics' | 'payment' | 'other'

const FORM_TABS: Array<{ id: FormTab; label: string; description: string }> = [
  { id: 'items', label: 'Item Details', description: 'Add and manage purchase order line items.' },
  { id: 'logistics', label: 'Logistics', description: 'Shipping, dispatch, and material movement references.' },
  { id: 'payment', label: 'Payment Terms', description: 'Define stage-wise payment terms for this order.' },
  { id: 'other', label: 'Other Terms', description: 'Commercial terms, warranty, and additional conditions.' },
]

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

function emptyPaymentTermDraft(): Omit<PaymentTermRow, 'id'> {
  return { type: '', basic: undefined, gst: undefined, stage: '', desc: '' }
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

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hydratedId, setHydratedId] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<FormTab>('items')
  const [form, setForm] = useState<Record<string, unknown>>({
    CardCode: '',
    CardName: '',
    Project: '',
    Comments: '',
    U_Owner: '',
    U_Stage: '',
    U_Warehouse: '',
    DocDate: todayIsoDate(),
    PostingDate: todayIsoDate(),
    DocDueDate: todayIsoDate(),
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
  const [projectLabel, setProjectLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [branchOptions, setBranchOptions] = useState<SelectOption[]>([])

  const loading = Boolean(id) && (queryLoading || hydratedId !== String(id))
  const loadError = error
    ?? (queryError instanceof Error ? queryError.message : queryError ? 'Failed to load purchase order' : null)

  const defaultWarehouse = String(form.U_Warehouse ?? '')

  const totals = useMemo(
    () => calculatePurchaseOrderTotals(lines, Number(form.RoundingDiffAmount ?? 0)),
    [lines, form.RoundingDiffAmount],
  )

  const summaryColumns: SapColumn<PurchaseOrderLineItem>[] = [
    { key: 'ItemCode', header: 'Item', accessor: (r) => formatCodeWithName(r.ItemCode, r.ItemDescription) },
    { key: 'UnitPrice', header: 'Unit Price', accessor: (r) => formatPoAmount(r.UnitPrice) },
    { key: 'Quantity', header: 'Purchase Qty', accessor: (r) => r.Quantity },
    { key: 'UomName', header: 'Uom Name', accessor: (r) => r.UomName ?? '—' },
    { key: 'StockQty', header: 'Stock Qty', accessor: (r) => r.StockQty ?? '—' },
    { key: 'Uom', header: 'Uom', accessor: (r) => r.UomName ?? '—' },
    { key: 'WeightKg', header: 'Weight (Kg)', accessor: (r) => formatPoAmount(r.WeightKg) },
    { key: 'TaxableAmount', header: 'Taxable Amount', accessor: (r) => formatPoAmount(r.TaxableAmount ?? r.LineTotal) },
  ]

  const searchVendorOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchVendors(search)
    return (response.data ?? []).map((v) => ({
      value: v.CardCode ?? '',
      label: `${v.CardCode ?? ''} - ${v.CardName ?? ''}`.trim(),
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
      label: `${wh.WarehouseCode ?? ''}${wh.City ? ` - ${wh.City}` : ''}`.trim(),
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
      setForm(record)
      setLines((purchaseOrder.DocumentLines as PurchaseOrderLineItem[] | undefined) ?? [])
      setPaymentTerms(parsePaymentTermsFromPo(record))
      setLogistics(readLogisticsFromPo(record))
      setOtherTerms(readOtherTermsFromPo(record))
      try {
        const labels = await resolveMasterSelectLabels({
          vendorCode: purchaseOrder.CardCode,
          projectCode: purchaseOrder.Project,
        })
        if (cancelled) return
        if (purchaseOrder.CardCode) {
          setVendorLabel(labels.vendorLabel ?? formatCodeWithName(purchaseOrder.CardCode, purchaseOrder.CardName))
        }
        if (purchaseOrder.Project) {
          setProjectLabel(labels.projectLabel ?? formatCodeWithName(purchaseOrder.Project))
        }
      } catch {
        // labels are optional enrichments
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
    if (!paymentDraft.type && paymentDraft.basic == null && paymentDraft.gst == null && !paymentDraft.stage) {
      setError('Enter at least type, percentage, or stage for the payment term.')
      return
    }
    setPaymentTerms([
      ...paymentTerms,
      {
        id: slot,
        type: paymentDraft.type || undefined,
        basic: paymentDraft.basic,
        gst: paymentDraft.gst,
        stage: paymentDraft.stage || undefined,
        desc: paymentDraft.desc || undefined,
      },
    ])
    setPaymentDraft(emptyPaymentTermDraft())
    setError(null)
  }

  const handleRemovePaymentTerm = (termId: number) => {
    setPaymentTerms(paymentTerms.filter((term) => term.id !== termId))
  }

  const buildPayload = (): PurchaseOrder => {
    let payload: Record<string, unknown> = {
      ...form,
      DocumentLines: lines,
      PostingDate: form.PostingDate ?? new Date().toISOString(),
      DocDueDate: form.DocDueDate ?? form.DueDate,
      BPLId: form.BPLId ?? authBranchId ?? 1,
      DocTotal: totals.totalPaymentDue,
      VatSum: totals.tax,
      RoundingDiffAmount: totals.roundingOff,
      Comments: form.Comments,
      U_Owner: form.U_Owner,
      U_Stage: form.U_Stage,
      U_Warehouse: form.U_Warehouse,
    }
    payload = applyPaymentTermsToPo(payload, paymentTerms)
    payload = applyLogisticsToPo(payload, logistics)
    payload = applyOtherTermsToPo(payload, otherTerms)
    return payload as PurchaseOrder
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!lines.length) {
      setError('Add at least one line item.')
      setActiveTab('items')
      return
    }
    if (!form.CardCode) {
      setError('Select a business partner.')
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
      await invalidatePurchaseOrders(id)
      navigate(ROUTES.PURCHASE_ORDERS)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
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
                    setVendorLabel(label)
                    updateForm({ CardCode: cardCode, CardName: cardName })
                  }}
                />
                <Input
                  label="Posting Date"
                  type="date"
                  value={String(form.PostingDate ?? form.DocDate ?? '').slice(0, 10)}
                  onChange={(e) => updateForm({ PostingDate: e.target.value, DocDate: e.target.value })}
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
                  value={String(form.DocDueDate ?? form.DueDate ?? '').slice(0, 10)}
                  onChange={(e) => updateForm({ DocDueDate: e.target.value, DueDate: e.target.value })}
                />
                <Input
                  label="Stage"
                  value={String(form.U_Stage ?? '')}
                  onChange={(e) => updateForm({ U_Stage: e.target.value })}
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
              </div>
            </section>

            <section className="space-y-3">
              <h3 className="text-sm font-semibold text-slate-700">Items Summary</h3>
              <SapDataGrid
                columns={summaryColumns}
                data={lines}
                getRowKey={(row) => `${row.ItemCode}-${row.WarehouseCode}-${lines.indexOf(row)}`}
                emptyMessage="No items added yet."
              />
            </section>

            <section>
              <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as FormTab)}>
                <TabsList aria-label="Purchase order sections" className="-mx-1 px-1">
                  <TabsTrigger value="items" icon={<Package className="h-4 w-4" />} badge={lines.length}>
                    Item Details
                  </TabsTrigger>
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
                  value="items"
                  title={FORM_TABS[0].label}
                  description={FORM_TABS[0].description}
                >
                  <PurchaseOrderLinesEditor
                    lines={lines}
                    onChange={setLines}
                    defaultWarehouse={defaultWarehouse}
                  />
                </TabsContent>

                <TabsContent
                  value="logistics"
                  title={FORM_TABS[1].label}
                  description={FORM_TABS[1].description}
                >
                  <div className="grid gap-4 md:grid-cols-2">
                  <Input
                    label="Dispatch To"
                    value={logistics.dispatchTo ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, dispatchTo: e.target.value })}
                  />
                  <Input
                    label="Contact Person"
                    value={logistics.contactPerson ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, contactPerson: e.target.value })}
                  />
                  <Input
                    label="Price Basis"
                    value={logistics.priceBasis ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, priceBasis: e.target.value })}
                  />
                  <Input
                    label="Mode of Transport"
                    value={logistics.modeOfTransport ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, modeOfTransport: e.target.value })}
                  />
                  <Input
                    label="Material Outward Document"
                    value={logistics.materialOutwardDoc ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, materialOutwardDoc: e.target.value })}
                  />
                  <Input
                    label="Goods Issue / Inventory Transfer"
                    value={logistics.goodsIssueTransfer ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, goodsIssueTransfer: e.target.value })}
                  />
                  <Input
                    label="Material Inward Document"
                    value={logistics.materialInwardDoc ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, materialInwardDoc: e.target.value })}
                  />
                  <Input
                    label="Goods Receipt / Inventory Transfer"
                    value={logistics.goodsReceiptTransfer ?? ''}
                    onChange={(e) => setLogistics({ ...logistics, goodsReceiptTransfer: e.target.value })}
                  />
                </div>
                </TabsContent>

                <TabsContent
                  value="payment"
                  title={FORM_TABS[2].label}
                  description={FORM_TABS[2].description}
                >
                <div className="space-y-4">
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
                    <Select
                      label="Type"
                      options={PAYMENT_TERM_TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                      value={paymentDraft.type ?? ''}
                      onChange={(value) => setPaymentDraft({ ...paymentDraft, type: value })}
                      placeholder="Select type"
                    />
                    <Input
                      label="Basic %"
                      type="number"
                      min="0"
                      nonNegative
                      value={paymentDraft.basic != null ? String(paymentDraft.basic) : ''}
                      onChange={(e) => setPaymentDraft({ ...paymentDraft, basic: e.target.value === '' ? undefined : Number(e.target.value) })}
                    />
                    <Input
                      label="GST %"
                      type="number"
                      min="0"
                      nonNegative
                      value={paymentDraft.gst != null ? String(paymentDraft.gst) : ''}
                      onChange={(e) => setPaymentDraft({ ...paymentDraft, gst: e.target.value === '' ? undefined : Number(e.target.value) })}
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
                          <th className="px-3 py-2 font-medium">Basic %</th>
                          <th className="px-3 py-2 font-medium">GST %</th>
                          <th className="px-3 py-2 font-medium">Stage</th>
                          <th className="px-3 py-2 font-medium">Description</th>
                          <th className="px-3 py-2 font-medium">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {paymentTerms.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="px-3 py-6 text-center text-slate-500">No payment terms added.</td>
                          </tr>
                        ) : paymentTerms.map((term) => (
                          <tr key={term.id} className="border-t border-slate-100">
                            <td className="px-3 py-2">{term.id}</td>
                            <td className="px-3 py-2">{term.type ?? '—'}</td>
                            <td className="px-3 py-2">{term.basic ?? '—'}</td>
                            <td className="px-3 py-2">{term.gst ?? '—'}</td>
                            <td className="px-3 py-2">{term.stage ?? '—'}</td>
                            <td className="px-3 py-2">{paymentTermDisplayLabel(term)}</td>
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
                  title={FORM_TABS[3].label}
                  description={FORM_TABS[3].description}
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
                <Textarea
                  label="User Remarks"
                  value={String(form.Comments ?? '')}
                  onChange={(e) => updateForm({ Comments: e.target.value })}
                />
                <Input
                  label="Owner"
                  value={String(form.U_Owner ?? '')}
                  onChange={(e) => updateForm({ U_Owner: e.target.value })}
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
