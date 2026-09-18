import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Package } from 'lucide-react'
import { PurchaseRequestLinesEditor } from '@/Components/forms/PurchaseRequestLinesEditor'
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
import { SAP_DECIMAL_PLACES } from '@/helpers/sapDecimals'
import { formatCodeWithName, resolveItem, resolveMasterSelectLabels } from '@/helpers/masterLookup'
import { formatPoDisplayDate, parsePoDisplayDate, todayIsoDate, toIsoDateOnly } from '@/helpers/lib/utils'
import {
  applyDocumentSpecialLinesToFormLines,
  applyLogisticsToPo,
  applyWarehouseToPoLines,
  calculatePurchaseRequestTotals,
  dispatchAddressSourceForLocation,
  dispatchLocationForWarehouse,
  formatPoAmount,
  normalizePurchaseRequestHeader,
  normalizePurchaseRequestLineFromApi,
  PO_DISPATCH_LOCATION_OPTIONS,
  readLogisticsFromPo,
  resolvePurchaseUnit,
  toDocumentSpecialLines,
  toSapDocumentLine,
  firstPositiveLocationCode,
  isPurchaseRequestReadOnly,
  usesBranchDispatchLocationMapping,
  warehouseForDispatchLocation,
} from '@/helpers/purchaseRequestForm'
import { useAppSelector } from '@/store/hooks'
import { getBranchesApi } from '@/Requests/auth'
import {
  fetchBusinessPartnerLogistics,
  getWarehouseAddress,
  searchBusinessPartners,
  searchProjects,
  searchWarehouses,
  formatWarehouseOptionLabel,
  lookupBusinessPartner,
  lookupHsnLabels,
  lookupSacLabels,
  type BusinessPartnerAddressOption,
  type MasterWarehouse,
} from '@/Requests/masters'
import {
  createPurchaseRequest,
  updatePurchaseRequest,
  cancelPurchaseRequest,
  downloadPurchaseRequestPdf,
  type PurchaseRequest,
} from '@/Requests/purchaseRequests'
import {
  useInvalidatePurchaseRequests,
  usePurchaseRequest,
} from '@/hooks/usePurchaseRequests'
import { toast } from '@/helpers/toast'
import {
  PO_DOC_TYPE,
  PO_DOC_TYPE_OPTIONS,
  isServicePoDocType,
} from '@/helpers/purchaseRequestTnValidation'
import type { SelectOption } from '@/types'
import type { PurchaseOrderLogistics, PurchaseRequestLineItem } from '@/types/purchaseRequest'

export function PurchaseRequestFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const authBranchId = useAppSelector((state) => state.auth.branchId)
  const authUserName = useAppSelector((state) => state.auth.user?.name)
  const authLoginName = useAppSelector((state) => state.auth.user?.userName)
  const invalidatePurchaseRequests = useInvalidatePurchaseRequests()
  const {
    data: purchaseRequest,
    isLoading: queryLoading,
    error: queryError,
  } = usePurchaseRequest(id)

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hydratedId, setHydratedId] = useState<string | null>(null)
  const [form, setForm] = useState<Record<string, unknown>>({
    CardCode: '',
    CardName: '',
    Project: '',
    Comments: '',
    NumAtCard: '',
    DocType: PO_DOC_TYPE.items,
    Requester: '',
    RequesterName: '',
    ReqType: 12,
    DocDate: todayIsoDate(),
    PostingDate: todayIsoDate(),
    TaxDate: todayIsoDate(),
    DocDueDate: '',
    DueDate: '',
    BPLId: authBranchId ?? 1,
    RoundingDiffAmount: 0,
    U_Warehouse: '',
    DocumentLines: [],
  })
  const formBranchId = Number(form.BPLId) || authBranchId
  const usesDispatchLocationMapping = usesBranchDispatchLocationMapping(formBranchId)
  const [lines, setLines] = useState<PurchaseRequestLineItem[]>([])

  useEffect(() => {
    if (id) return
    const sapUserCode = sapUserCodeFromLogin(authLoginName)
    if (!sapUserCode) return
    setForm((prev) => (prev.Requester
      ? prev
      : { ...prev, Requester: sapUserCode, RequesterName: authUserName || sapUserCode, ReqType: 12 }))
  }, [id, authLoginName, authUserName])

  const [projectLabel, setProjectLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [dispatchLocation, setDispatchLocation] = useState('')
  const [logistics, setLogistics] = useState<PurchaseOrderLogistics>({})
  const [dispatchToLabel, setDispatchToLabel] = useState('')
  const [dispatchAddressOptions, setDispatchAddressOptions] = useState<BusinessPartnerAddressOption[]>([])
  const [branchOptions, setBranchOptions] = useState<SelectOption[]>([])
  const [postingDateDisplay, setPostingDateDisplay] = useState(() => formatPoDisplayDate(todayIsoDate()))
  const [deliveryDateDisplay, setDeliveryDateDisplay] = useState('')

  const loading = Boolean(id) && (queryLoading || hydratedId !== String(id))
  const loadError = error
    ?? (queryError instanceof Error ? queryError.message : queryError ? 'Failed to load purchase request' : null)

  const defaultWarehouse = String(form.U_Warehouse ?? '')
  const docType = String(form.DocType ?? PO_DOC_TYPE.items)
  const isServiceDoc = isServicePoDocType(docType)
  const isReadOnly = Boolean(id) && isPurchaseRequestReadOnly(form)

  const totals = useMemo(
    () => calculatePurchaseRequestTotals(lines, Number(form.RoundingDiffAmount ?? 0)),
    [lines, form.RoundingDiffAmount],
  )

  const searchBusinessPartnerOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchBusinessPartners(search)
    return (response.data ?? []).map((bp) => ({
      value: bp.CardCode ?? '',
      label: `${bp.CardCode ?? ''} - ${bp.CardName ?? ''}`.trim(),
      meta: bp,
    })).filter((o) => o.value)
  }, [])

  const loadDispatchLogisticsOptions = useCallback(async (cardCode: string, preferDefaults: boolean) => {
    if (!cardCode.trim()) {
      setDispatchAddressOptions([])
      return
    }
    const details = await fetchBusinessPartnerLogistics(cardCode)
    const addresses = details?.addresses ?? []
    setDispatchAddressOptions(addresses)

    if (!preferDefaults) return

    setLogistics((prev) => {
      const next = { ...prev, dispatchTo: cardCode }
      if (!prev.dispatchAddress) {
        const shipName = (details?.defaultShipTo ?? '').trim()
        const shipAddr = shipName
          ? addresses.find((a) => a.addressName === shipName)
          : undefined
        const fallback = shipAddr ?? addresses.find((a) => /ship/i.test(a.addressType)) ?? addresses[0]
        if (fallback?.formattedAddress) next.dispatchAddress = fallback.formattedAddress
      }
      return next
    })
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
      meta: wh,
    })).filter((o) => o.value)
  }, [])

  const applyWarehouseToLines = useCallback((warehouse: string, location?: number) => {
    setWarehouseLabel(warehouse)
    setForm((prev) => ({ ...prev, U_Warehouse: warehouse }))
    if (location != null && location > 0) {
      setLines((prev) => applyWarehouseToPoLines(prev, warehouse, location))
      return
    }
    setLines((prev) => prev.map((line) => ({ ...line, WarehouseCode: warehouse || line.WarehouseCode })))
    if (!warehouse) return
    void searchWarehouses(warehouse, 20).then((response) => {
      const match = (response.data ?? []).find((wh) => wh.WarehouseCode === warehouse)
      const loc = match?.Location
      if (loc == null || !Number.isFinite(loc) || loc <= 0) return
      setLines((prev) => applyWarehouseToPoLines(prev, warehouse, loc))
    })
  }, [])

  const applyDispatchLocation = useCallback((location: string) => {
    setDispatchLocation(location)
    const warehouse = warehouseForDispatchLocation(formBranchId, location)
    if (!warehouse) return
    applyWarehouseToLines(warehouse)

    if (dispatchAddressSourceForLocation(formBranchId, location) === 'whse') {
      void getWarehouseAddress(warehouse).then((address) => {
        if (address?.formattedAddress) {
          setLogistics((prev) => ({ ...prev, dispatchAddress: address.formattedAddress }))
        }
      })
    }
  }, [applyWarehouseToLines, formBranchId])

  const resolveIndiaCodeLabels = useCallback(async (loaded: PurchaseRequestLineItem[]) => {
    const hsnEntries = loaded.filter((line) => !line.HsnLabel && line.HSNEntry != null).map((line) => line.HSNEntry!)
    const sacEntries = loaded.filter((line) => !line.SacLabel && line.SACEntry != null).map((line) => line.SACEntry!)
    if (hsnEntries.length === 0 && sacEntries.length === 0) return
    const empty: Record<number, string> = {}
    const [hsnLabels, sacLabels] = await Promise.all([
      hsnEntries.length > 0 ? lookupHsnLabels(hsnEntries) : Promise.resolve(empty),
      sacEntries.length > 0 ? lookupSacLabels(sacEntries) : Promise.resolve(empty),
    ])
    setLines((prev) => prev.map((line) => ({
      ...line,
      HsnLabel: line.HsnLabel ?? (line.HSNEntry != null ? hsnLabels[line.HSNEntry] : undefined),
      SacLabel: line.SacLabel ?? (line.SACEntry != null ? sacLabels[line.SACEntry] : undefined),
    })))
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
      setDispatchLocation('')
      setLogistics({})
      setDispatchToLabel('')
      setDispatchAddressOptions([])
      setPostingDateDisplay(formatPoDisplayDate(todayIsoDate()))
      setDeliveryDateDisplay('')
      return
    }
    if (!purchaseRequest || queryLoading)
      return

    let cancelled = false
    void (async () => {
      const record = normalizePurchaseRequestHeader(purchaseRequest as Record<string, unknown>)
      const loadedLogistics = readLogisticsFromPo(record)
      setForm({
        ...record,
        DocType: record.DocType || PO_DOC_TYPE.items,
      })
      setLogistics(loadedLogistics)
      setDispatchToLabel('')
      setDispatchAddressOptions([])
      setPostingDateDisplay(formatPoDisplayDate(String(record.DocDate ?? record.PostingDate ?? '')))
      setDeliveryDateDisplay(formatPoDisplayDate(String(
        record.DocDueDate ?? record.DueDate ?? record.RequiredDate ?? record.RequriedDate ?? '',
      )))
      const rawLines = (purchaseRequest.DocumentLines as PurchaseRequestLineItem[] | undefined) ?? []
      const loadedLines = applyDocumentSpecialLinesToFormLines(
        rawLines.map((line) => {
          const normalized = normalizePurchaseRequestLineFromApi(line)
          return {
            ...normalized,
            UoMCode: resolvePurchaseUnit(normalized) || normalized.UoMCode,
          }
        }),
        (purchaseRequest as { DocumentSpecialLines?: Array<{ AfterLineNumber?: number; LineText?: string }> }).DocumentSpecialLines,
      )
      setLines(loadedLines)
      void resolveIndiaCodeLabels(loadedLines)
      void (async () => {
        const codes = [...new Set(loadedLines.map((line) => line.ItemCode?.trim()).filter(Boolean))] as string[]
        if (codes.length === 0) return
        const entries = await Promise.all(codes.map(async (code) => {
          const item = await resolveItem(code)
          return [code, item?.InventoryItem] as const
        }))
        const flags = Object.fromEntries(entries.filter(([, flag]) => flag))
        if (cancelled || Object.keys(flags).length === 0) return
        setLines((prev) => prev.map((line) => {
          const code = line.ItemCode?.trim()
          const flag = code ? flags[code] : undefined
          return flag ? { ...line, InventoryItem: flag } : line
        }))
      })()
      try {
        const [labels, dispatchBp] = await Promise.all([
          resolveMasterSelectLabels({
            projectCode: String(record.Project ?? purchaseRequest.Project ?? ''),
          }),
          loadedLogistics.dispatchTo
            ? lookupBusinessPartner(loadedLogistics.dispatchTo)
            : Promise.resolve(undefined),
        ])
        if (cancelled) return
        const projectCode = String(record.Project ?? purchaseRequest.Project ?? '')
        if (projectCode) {
          setProjectLabel(labels.projectLabel ?? formatCodeWithName(projectCode))
        }
        if (loadedLogistics.dispatchTo) {
          setDispatchToLabel(
            dispatchBp
              ? formatCodeWithName(dispatchBp.CardCode, dispatchBp.CardName)
              : formatCodeWithName(loadedLogistics.dispatchTo),
          )
          void loadDispatchLogisticsOptions(loadedLogistics.dispatchTo, false)
        }
      } catch {
        // labels are optional enrichments
      }
      if (cancelled) return
      const wh = String(record.U_Warehouse ?? '')
      if (wh) {
        setWarehouseLabel(wh)
        const recordBranchId = Number(record.BPLId) || authBranchId
        if (usesBranchDispatchLocationMapping(recordBranchId)) {
          setDispatchLocation(dispatchLocationForWarehouse(recordBranchId, wh) ?? '')
        }
      }
      setHydratedId(String(id))
    })()

    return () => {
      cancelled = true
    }
  }, [id, purchaseRequest, queryLoading, authBranchId, resolveIndiaCodeLabels, loadDispatchLogisticsOptions])

  const buildPayload = (): PurchaseRequest => {
    const docDate = parsePoDisplayDate(postingDateDisplay)
      ?? toIsoDateOnly(String(form.DocDate ?? form.PostingDate ?? ''))
      ?? todayIsoDate()
    const docDue = parsePoDisplayDate(deliveryDateDisplay)
      ?? toIsoDateOnly(String(form.DocDueDate ?? form.DueDate ?? ''))
      ?? ''
    const taxDate = docDate
    const payload: Record<string, unknown> = {
      ...form,
      DocumentLines: lines.map((line, index) => toSapDocumentLine(line, {
        isService: isServiceDoc,
        fallbackProject: form.Project ? String(form.Project) : undefined,
        lineIndex: id ? undefined : index,
        fallbackLocationCode: isServiceDoc ? firstPositiveLocationCode(lines) : undefined,
        requiredDate: docDue || undefined,
      })),
      DocumentSpecialLines: toDocumentSpecialLines(lines),
      DocType: docType,
      DocDate: docDate,
      DocDueDate: docDue,
      TaxDate: taxDate,
      BPL_IDAssignedToInvoice: form.BPLId ?? authBranchId ?? 1,
      BPLId: form.BPLId ?? authBranchId ?? 1,
      NumAtCard: form.NumAtCard || undefined,
      Comments: form.Comments,
      Requester: form.Requester || undefined,
      RequesterName: form.RequesterName || authUserName || undefined,
      ReqType: form.ReqType != null ? Number(form.ReqType) : 12,
      RequriedDate: docDue || undefined,
      RoundingDiffAmount: totals.roundingOff,
    }
    Object.assign(payload, applyLogisticsToPo(payload, logistics))
    delete payload.ReqCode
    delete payload.DocTotal
    delete payload.VatSum
    delete payload.PostingDate
    delete payload.DueDate
    delete payload.ShipToCode
    delete payload.SalesPersonCode
    delete payload.DocumentsOwner
    delete payload.U_PO_Type
    delete payload.U_TRN
    delete payload.U_Owner
    delete payload.U_Stage
    return payload as PurchaseRequest
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (isReadOnly) {
      toast.error('Cancelled purchase requests cannot be updated.')
      return
    }
    if (!lines.length) {
      setError(isServiceDoc ? 'Add at least one service line.' : 'Add at least one line item.')
      toast.error(isServiceDoc ? 'Add at least one service line.' : 'Add at least one line item.')
      return
    }
    const deliveryDate = parsePoDisplayDate(deliveryDateDisplay)
      ?? toIsoDateOnly(String(form.DocDueDate ?? form.DueDate ?? ''))
    if (!deliveryDate) {
      setError('Required Date is required.')
      toast.error('Required Date is required.')
      return
    }
    const requester = String(form.Requester ?? '').trim()
    if (requester && !looksLikeSapUserCode(requester)) {
      setError('Requester must be a SAP user code (for example manager), not a display name.')
      toast.error('Requester must be a SAP user code (for example manager), not a display name.')
      return
    }
    if (usesDispatchLocationMapping && !dispatchLocation.trim()) {
      setError('Dispatch Location is required.')
      toast.error('Dispatch Location is required.')
      return
    }
    if (!String(logistics.dispatchTo ?? '').trim() || !String(logistics.dispatchAddress ?? '').trim()) {
      setError('Dispatch To and Dispatch Address are required.')
      toast.error('Dispatch To and Dispatch Address are required.')
      return
    }
    if (!isServiceDoc && lines.some((line) => !String(line.WarehouseCode ?? '').trim())) {
      setError('Each item line needs a warehouse.')
      toast.error('Each item line needs a warehouse.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const payload = buildPayload()
      const result = id
        ? await updatePurchaseRequest(Number(id), payload)
        : await createPurchaseRequest(payload)

      if (result?.pendingApproval) {
        toast.info(
          id
            ? 'Purchase request update submitted for approval.'
            : 'Purchase request submitted for approval. It will appear in SAP after approval.',
        )
        await invalidatePurchaseRequests(id)
        navigate(ROUTES.MY_APPROVAL_REQUESTS, {
          state: {
            message: id
              ? 'Purchase request update submitted for approval. It will sync to SAP after approval.'
              : 'Purchase request submitted for approval. It will appear in SAP after approval.',
            approvalRequestId: result.pendingApprovalRequestId,
          },
        })
        return
      }

      const sapError =
        typeof result?.error === 'object' && result.error !== null
          ? ((result.error as { message?: { value?: string } }).message?.value
            ?? 'SAP rejected the purchase request.')
          : null
      if (sapError) {
        setError(sapError)
        toast.error(sapError)
        return
      }
      if (!id && result?.DocNum == null) {
        const message = 'Purchase request was not created in SAP (missing document number).'
        setError(message)
        toast.error(message)
        return
      }

      toast.success(
        result?.DocNum != null
          ? `Purchase request ${result.DocNum} saved.`
          : 'Purchase request saved.',
      )
      await invalidatePurchaseRequests(id)
      navigate(ROUTES.PURCHASE_REQUESTS)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Save failed'
      setError(message)
      toast.error(message)
    } finally {
      setSaving(false)
    }
  }

  const updateForm = (patch: Record<string, unknown>) => setForm((prev) => ({ ...prev, ...patch }))

  const handleCancelDocument = async () => {
    if (!id) return
    const ok = window.confirm('Cancel this purchase request in SAP? This cannot be undone.')
    if (!ok) return
    setSaving(true)
    setError(null)
    try {
      await cancelPurchaseRequest(Number(id))
      toast.success('Purchase request cancelled in SAP.')
      await invalidatePurchaseRequests(id)
      navigate(ROUTES.PURCHASE_REQUESTS)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Cancel failed'
      setError(message)
      toast.error(message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="min-w-0 space-y-6">
      <PageHeader
        title={id ? 'Edit Purchase Request' : 'New Purchase Request'}
        action={id ? (
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => void downloadPurchaseRequestPdf(Number(id))}
            >
              Download PDF
            </Button>
            {!isReadOnly && (
              <Button
                type="button"
                variant="outline"
                data-testid="purchase-request-cancel-document"
                onClick={() => void handleCancelDocument()}
              >
                Cancel in SAP
              </Button>
            )}
          </div>
        ) : undefined}
      />
      <BlockingLoader
        visible={loading || saving}
        label={loading ? 'Loading purchase request...' : 'Saving purchase request...'}
        lockScroll={false}
      />
      {isReadOnly && (
        <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
          This purchase request is cancelled in SAP and cannot be updated.
        </div>
      )}
      {loadError && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{loadError}</div>}

      <Card>
        <CardContent className="space-y-6 pt-6">
          <form onSubmit={handleSubmit} className="space-y-6" data-testid="purchase-request-form">
            <section className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <Input
                  label="Requester"
                  data-testid="purchase-request-requester"
                  value={String(form.Requester ?? '')}
                  disabled={isReadOnly}
                  onChange={(e) => updateForm({ Requester: e.target.value, ReqType: 12 })}
                  placeholder="SAP user code (e.g. manager)"
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
                <Input
                  label="Posting Date"
                  placeholder="DD/MM/YYYY"
                  value={postingDateDisplay}
                  disabled={isReadOnly}
                  onChange={(e) => setPostingDateDisplay(e.target.value)}
                  onBlur={() => {
                    const iso = parsePoDisplayDate(postingDateDisplay)
                      ?? toIsoDateOnly(postingDateDisplay)
                      ?? todayIsoDate()
                    setPostingDateDisplay(formatPoDisplayDate(iso))
                    updateForm({ DocDate: iso, PostingDate: iso, TaxDate: iso })
                  }}
                />
                <SearchableSelect
                  label="Project"
                  lookupKind="project"
                  value={String(form.Project ?? '')}
                  selectedLabel={projectLabel}
                  disabled={isReadOnly}
                  placeholder="Search project by code or name..."
                  onSearch={searchProjectOptions}
                  onChange={(projectCode, option) => {
                    setProjectLabel(option?.label ?? projectCode)
                    updateForm({ Project: projectCode })
                  }}
                />
                <Input
                  label="Required Date"
                  required
                  placeholder="DD/MM/YYYY"
                  value={deliveryDateDisplay}
                  disabled={isReadOnly}
                  onChange={(e) => setDeliveryDateDisplay(e.target.value)}
                  onBlur={() => {
                    const iso = parsePoDisplayDate(deliveryDateDisplay)
                      ?? toIsoDateOnly(deliveryDateDisplay)
                    if (!iso) {
                      setDeliveryDateDisplay('')
                      updateForm({ DocDueDate: '', DueDate: '' })
                      return
                    }
                    setDeliveryDateDisplay(formatPoDisplayDate(iso))
                    updateForm({ DocDueDate: iso, DueDate: iso })
                  }}
                />
                <Input
                  label="Vendor Ref."
                  value={String(form.NumAtCard ?? '')}
                  disabled={isReadOnly}
                  onChange={(e) => updateForm({ NumAtCard: e.target.value })}
                />
                {usesDispatchLocationMapping ? (
                  <Select
                    label="Dispatch Location"
                    required
                    options={[...PO_DISPATCH_LOCATION_OPTIONS]}
                    value={dispatchLocation}
                    disabled={isReadOnly}
                    onChange={(value) => applyDispatchLocation(value)}
                    placeholder="Factory / Office / Customer Loc / SubContractor Loc"
                  />
                ) : null}
                <SearchableSelect
                  label="Warehouse"
                  value={String(form.U_Warehouse ?? '')}
                  selectedLabel={warehouseLabel}
                  disabled={isReadOnly}
                  placeholder="Search warehouse..."
                  onSearch={searchWarehouseOptions}
                  onChange={(code, option) => {
                    const loc = (option?.meta as MasterWarehouse | undefined)?.Location
                    setWarehouseLabel(option?.label ?? code)
                    applyWarehouseToLines(code, loc != null && Number.isFinite(loc) ? loc : undefined)
                    if (usesDispatchLocationMapping) {
                      const resolvedLocation = dispatchLocationForWarehouse(formBranchId, code) ?? ''
                      setDispatchLocation(resolvedLocation)
                      if (dispatchAddressSourceForLocation(formBranchId, resolvedLocation) === 'whse') {
                        void getWarehouseAddress(code).then((address) => {
                          if (address?.formattedAddress) {
                            setLogistics((prev) => ({ ...prev, dispatchAddress: address.formattedAddress }))
                          }
                        })
                      }
                    }
                  }}
                />
                <Select
                  label="Branch"
                  options={branchOptions}
                  value={String(form.BPLId ?? authBranchId ?? '')}
                  disabled={isReadOnly}
                  onChange={(value) => updateForm({ BPLId: Number(value) })}
                  placeholder="Select branch"
                />
                <SearchableSelect
                  label="Dispatch To / Ship To (BP Code & Name)"
                  lookupKind="businessPartner"
                  required
                  disabled={isReadOnly}
                  value={logistics.dispatchTo ?? ''}
                  selectedLabel={dispatchToLabel}
                  placeholder="Search BP code or name..."
                  onSearch={searchBusinessPartnerOptions}
                  onChange={(cardCode, option) => {
                    setDispatchToLabel(option?.label ?? cardCode)
                    setLogistics({
                      ...logistics,
                      dispatchTo: cardCode || undefined,
                      dispatchAddress: undefined,
                    })
                    void loadDispatchLogisticsOptions(cardCode, true)
                  }}
                />
                <Select
                  label="Address from BP"
                  options={dispatchAddressOptions.map((a) => ({
                    value: a.formattedAddress,
                    label: a.addressName
                      ? `${a.addressName}${a.addressType ? ` (${a.addressType.replace(/^bo_/, '')})` : ''} — ${a.formattedAddress}`
                      : a.formattedAddress,
                  }))}
                  value={
                    dispatchAddressOptions.some((a) => a.formattedAddress === (logistics.dispatchAddress ?? ''))
                      ? (logistics.dispatchAddress ?? '')
                      : ''
                  }
                  onChange={(value) => setLogistics({ ...logistics, dispatchAddress: value || undefined })}
                  placeholder={dispatchAddressOptions.length ? 'Select address...' : 'Select Dispatch To first'}
                  clearable
                  disabled={isReadOnly || !logistics.dispatchTo || dispatchAddressOptions.length === 0}
                />
                <Input
                  label="Dispatch Address"
                  required
                  value={logistics.dispatchAddress ?? ''}
                  disabled={isReadOnly}
                  onChange={(e) => setLogistics({ ...logistics, dispatchAddress: e.target.value.slice(0, 120) })}
                  hint="Maximum 120 characters."
                />
              </div>
            </section>

            <section>
              <Tabs value="items" onValueChange={() => undefined}>
                <TabsList aria-label="Purchase request contents" className="-mx-1 px-1">
                  <TabsTrigger value="items" icon={<Package className="h-4 w-4" />} badge={lines.length || undefined}>
                    {isServiceDoc ? 'Services' : 'Items'}
                  </TabsTrigger>
                </TabsList>
                <TabsContent value="items">
                  <PurchaseRequestLinesEditor
                    lines={lines}
                    onChange={setLines}
                    defaultWarehouse={defaultWarehouse}
                    defaultProject={String(form.Project ?? '')}
                    docType={docType}
                    assignLineNums={!id}
                    readOnly={isReadOnly}
                    columnLayout="purchaseRequest"
                  />
                </TabsContent>
              </Tabs>
            </section>

            <section className="grid gap-4 border-t border-slate-200 pt-4 md:grid-cols-2">
              <Textarea
                label="User Remarks"
                value={String(form.Comments ?? '')}
                disabled={isReadOnly}
                onChange={(e) => updateForm({ Comments: e.target.value })}
              />
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
                  decimalPlaces={SAP_DECIMAL_PLACES.amounts}
                  value={String(form.RoundingDiffAmount ?? 0)}
                  disabled={isReadOnly}
                  onChange={(e) => updateForm({ RoundingDiffAmount: Number(e.target.value) })}
                />
                <div className="flex items-center justify-between border-t border-slate-200 pt-3 text-base">
                  <span className="font-medium text-slate-700">Total Payment Due</span>
                  <span className="text-lg font-bold text-primary-700">{formatPoAmount(totals.totalPaymentDue)}</span>
                </div>
              </div>
            </section>

            <div className="flex flex-wrap items-center gap-3">
              <Button type="submit" isLoading={saving} disabled={isReadOnly} data-testid="purchase-request-submit">Submit</Button>
              <Button type="button" variant="outline" data-testid="purchase-request-back" onClick={() => navigate(ROUTES.PURCHASE_REQUESTS)}>Back</Button>
              <PreviousNextButtons
                id={id}
                onPrevious={id && Number(id) > 1 ? () => navigate(`/purchase-requests/form/${Number(id) - 1}`) : undefined}
                onNext={id ? () => navigate(`/purchase-requests/form/${Number(id) + 1}`) : undefined}
              />
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}

function looksLikeSapUserCode(value: string): boolean {
  const code = value.trim()
  return code.length > 0 && code.length <= 25 && !code.includes(' ') && !code.includes('@')
}

function sapUserCodeFromLogin(loginName: string | undefined): string {
  const code = loginName?.trim() ?? ''
  return looksLikeSapUserCode(code) ? code : ''
}
