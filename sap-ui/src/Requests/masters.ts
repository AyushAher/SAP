import { apiListPost } from '@/helpers/api/list'
import { createMasterSearchRequest } from '@/helpers/api/masterSearch'
import type { Filter, PaginationRequest, PaginationResponse } from '@/types/api'

export interface MasterItem {
  ItemCode?: string
  ItemName?: string
  InventoryUom?: string
  PurchaseUnit?: string
  /** Items per purchase unit from item master (NumInBuy). */
  PurchaseItemsPerUnit?: number
  InventoryWeight?: number
  /** India GST — typically HSN AbsEntry as string (SL may send number). */
  ChapterID?: string
  DefaultWarehouse?: string
  PurchaseVatGroup?: string
}

export interface MasterWarehouse {
  WarehouseCode?: string
  WarehouseName?: string
  City?: string
  State?: string
}

export interface MasterTaxCode {
  Code?: string
  Name?: string
  Rate?: number
}

export interface MasterProject {
  Code?: string
  Name?: string
}

export interface MasterGlAccount {
  Code?: string
  Name?: string
}

export interface MasterHsnCode {
  AbsEntry?: number
  ChapterID?: string
  Chapter?: string
  Heading?: string
  SubHeading?: string
  Description?: string
  DisplayLabel?: string
}

export interface MasterSacCode {
  AbsEntry?: number
  ServiceCode?: string
  Description?: string
  DisplayLabel?: string
}

export interface MasterBusinessPartner {
  CardCode?: string
  CardName?: string
  Series?: number
}

export interface MasterSalesPerson {
  SalesEmployeeCode?: number
  SalesEmployeeName?: string
}

export interface MasterEmployee {
  EmployeeID?: number
  FirstName?: string
  LastName?: string
  DisplayName?: string
  MobilePhone?: string
  OfficePhone?: string
  HomePhone?: string
  /** Prefer mobile, then office, then home. */
  ContactPhone?: string
}

/** Label/value for PO Contact Person → U_SHIPTO (name + phone). */
export function formatEmployeeShipToLabel(emp: Pick<MasterEmployee, 'DisplayName' | 'EmployeeID' | 'ContactPhone' | 'MobilePhone' | 'OfficePhone' | 'HomePhone'>): string {
  const name = (emp.DisplayName ?? '').trim() || String(emp.EmployeeID ?? '')
  const phone = (emp.ContactPhone ?? emp.MobilePhone ?? emp.OfficePhone ?? emp.HomePhone ?? '').trim()
  return phone ? `${name} (${phone})` : name
}

function normalizeEmployee(raw: Record<string, unknown>): MasterEmployee | undefined {
  const id = Number(raw.EmployeeID ?? raw.employeeID)
  if (!Number.isFinite(id)) return undefined
  const first = String(raw.FirstName ?? raw.firstName ?? '')
  const last = String(raw.LastName ?? raw.lastName ?? '')
  const name = [first, last].filter(Boolean).join(' ')
  const mobile = String(raw.MobilePhone ?? raw.mobilePhone ?? '').trim() || undefined
  const office = String(raw.OfficePhone ?? raw.officePhone ?? '').trim() || undefined
  const home = String(raw.HomePhone ?? raw.homePhone ?? '').trim() || undefined
  const contact = mobile || office || home
  return {
    EmployeeID: id,
    FirstName: first || undefined,
    LastName: last || undefined,
    DisplayName: name || String(id),
    MobilePhone: mobile,
    OfficePhone: office,
    HomePhone: home,
    ContactPhone: contact,
  }
}

export interface MasterLookupPayload {
  itemCodes?: string[]
  projectCodes?: string[]
  cardCodes?: string[]
}

export interface MasterLookupResult {
  items: Record<string, string | undefined>
  projects: Record<string, string | undefined>
  businessPartners: Record<string, string | undefined>
}

function normalizeItem(raw: Record<string, unknown> | MasterItem | undefined): MasterItem | undefined {
  if (!raw) return undefined
  const source = raw as Record<string, unknown>
  return {
    ItemCode: String(source.ItemCode ?? source.itemCode ?? ''),
    ItemName: String(source.ItemName ?? source.itemName ?? ''),
    InventoryUom: String(source.InventoryUom ?? source.inventoryUom ?? source.InventoryUOM ?? ''),
    PurchaseUnit: String(source.PurchaseUnit ?? source.purchaseUnit ?? '') || undefined,
    PurchaseItemsPerUnit: Number.isFinite(Number(source.PurchaseItemsPerUnit ?? source.purchaseItemsPerUnit))
      ? Number(source.PurchaseItemsPerUnit ?? source.purchaseItemsPerUnit)
      : undefined,
    InventoryWeight: Number(source.InventoryWeight ?? source.inventoryWeight ?? 0) || undefined,
    ChapterID: source.ChapterID != null || source.chapterID != null
      ? String(source.ChapterID ?? source.chapterID)
      : undefined,
    DefaultWarehouse: String(source.DefaultWarehouse ?? source.defaultWarehouse ?? '') || undefined,
    PurchaseVatGroup: String(source.PurchaseVatGroup ?? source.PurchaseVATGroup ?? source.purchaseVatGroup ?? '') || undefined,
  }
}

function normalizeHsn(raw: Record<string, unknown> | MasterHsnCode | undefined): MasterHsnCode | undefined {
  if (!raw) return undefined
  const source = raw as Record<string, unknown>
  const abs = Number(source.AbsEntry ?? source.absEntry)
  if (!Number.isFinite(abs)) return undefined
  const chapterID = String(source.ChapterID ?? source.chapterID ?? '')
  const chapter = String(source.Chapter ?? source.chapter ?? '')
  const heading = String(source.Heading ?? source.heading ?? '')
  const sub = String(source.SubHeading ?? source.subHeading ?? '')
  const desc = String(source.Description ?? source.description ?? source.Dscription ?? source.dscription ?? '')
  const code = chapterID || [chapter, heading, sub].filter(Boolean).join('')
  return {
    AbsEntry: abs,
    ChapterID: chapterID || undefined,
    Chapter: chapter || undefined,
    Heading: heading || undefined,
    SubHeading: sub || undefined,
    Description: desc || undefined,
    DisplayLabel: desc ? `${code} - ${desc}` : code || String(abs),
  }
}

function normalizeSac(raw: Record<string, unknown> | MasterSacCode | undefined): MasterSacCode | undefined {
  if (!raw) return undefined
  const source = raw as Record<string, unknown>
  const abs = Number(source.AbsEntry ?? source.absEntry)
  if (!Number.isFinite(abs)) return undefined
  const serviceCode = String(source.ServiceCode ?? source.serviceCode ?? '')
  const desc = String(source.Description ?? source.description ?? '')
  return {
    AbsEntry: abs,
    ServiceCode: serviceCode || undefined,
    Description: desc || undefined,
    DisplayLabel: desc ? `${serviceCode || abs} - ${desc}` : (serviceCode || String(abs)),
  }
}

function normalizeProject(raw: Record<string, unknown> | MasterProject | undefined): MasterProject | undefined {
  if (!raw) return undefined
  const source = raw as Record<string, unknown>
  const code = source.Code ?? source.code ?? source.ProjectCode ?? source.projectCode
  const name = source.Name ?? source.name ?? source.ProjectName ?? source.projectName
  if (!code) return undefined
  return { Code: String(code), Name: name ? String(name) : undefined }
}

function normalizeBusinessPartner(raw: Record<string, unknown> | MasterBusinessPartner | undefined): MasterBusinessPartner | undefined {
  if (!raw) return undefined
  const source = raw as Record<string, unknown>
  const cardCode = source.CardCode ?? source.cardCode
  if (!cardCode) return undefined
  const seriesRaw = source.Series ?? source.series
  const series = seriesRaw == null || seriesRaw === '' ? undefined : Number(seriesRaw)
  return {
    CardCode: String(cardCode),
    CardName: String(source.CardName ?? source.cardName ?? ''),
    Series: Number.isFinite(series) ? series : undefined,
  }
}

function normalizeLookupResult(raw: Record<string, unknown>): MasterLookupResult {
  const readMap = (value: unknown) => (value && typeof value === 'object' ? value as Record<string, string> : {})
  return {
    items: readMap(raw.items ?? raw.Items),
    projects: readMap(raw.projects ?? raw.Projects),
    businessPartners: readMap(raw.businessPartners ?? raw.BusinessPartners),
  }
}

async function searchMaster<T>(
  url: string,
  search: string,
  pageSize = 20,
  fields?: string[],
  extraFilters: Filter[] = [],
) {
  const request = createMasterSearchRequest(search, { pageSize, fields })
  if (extraFilters.length > 0)
    request.filters = [...(request.filters ?? []), ...extraFilters]
  return apiListPost<T>(url, request)
}

export const CONSUMABLES_ITEM_GROUP_FILTER: Filter = {
  field: 'ItemsGroupName',
  operator: 'contains',
  value: 'Consumable',
}

export const ITEM_DROPDOWN_FIELDS = ['ItemCode', 'ItemName']
/**
 * Fields needed by line editors that also resolve UOM for the selected item.
 */
export const ITEM_DETAIL_FIELDS = [
  'ItemCode',
  'ItemName',
  'InventoryUOM',
  'PurchaseUnit',
  'PurchaseItemsPerUnit',
  'InventoryWeight',
  'PurchaseVATGroup',
  'ChapterID',
  'DefaultWarehouse',
]
export const WAREHOUSE_DROPDOWN_FIELDS = ['WarehouseCode', 'WarehouseName']
export const TAX_CODE_DROPDOWN_FIELDS = ['Code', 'Name', 'Rate']
export const PROJECT_DROPDOWN_FIELDS = ['Code', 'Name']
export const GL_ACCOUNT_DROPDOWN_FIELDS = ['Code', 'Name']

export function searchItems(
  search: string,
  pageSize = 20,
  fields: string[] = ITEM_DROPDOWN_FIELDS,
  extraFilters: Filter[] = [],
) {
  return searchMaster<MasterItem>('/masters/items/list', search, pageSize, fields, extraFilters).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeItem(row as MasterItem)).filter(Boolean) as MasterItem[],
  }))
}

export function searchWarehouses(search: string, pageSize = 20, fields: string[] = WAREHOUSE_DROPDOWN_FIELDS) {
  return searchMaster<MasterWarehouse>('/masters/warehouses/list', search, pageSize, fields)
}

/** Dropdown label: WarehouseCode - WarehouseName */
export function formatWarehouseOptionLabel(wh: MasterWarehouse): string {
  const code = wh.WarehouseCode?.trim() ?? ''
  const name = wh.WarehouseName?.trim()
  if (!code) return name || ''
  if (!name || name === code) return code
  return `${code} - ${name}`
}

export function searchTaxCodes(search: string, pageSize = 20, fields: string[] = TAX_CODE_DROPDOWN_FIELDS) {
  return searchMaster<MasterTaxCode>('/masters/tax-codes/list', search, pageSize, fields)
}

export function searchProjects(search: string, pageSize = 20, fields: string[] = PROJECT_DROPDOWN_FIELDS) {
  return searchMaster<MasterProject>('/masters/projects/list', search, pageSize, fields)
}

export function searchGlAccounts(search: string, pageSize = 20, fields: string[] = GL_ACCOUNT_DROPDOWN_FIELDS) {
  return searchMaster<MasterGlAccount>('/masters/gl-accounts/list', search, pageSize, fields).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => {
      const source = row as Record<string, unknown>
      const code = String(source.Code ?? source.code ?? '')
      if (!code) return undefined
      return {
        Code: code,
        Name: String(source.Name ?? source.name ?? '') || undefined,
      } satisfies MasterGlAccount
    }).filter(Boolean) as MasterGlAccount[],
  }))
}

export function searchHsnCodes(search: string, pageSize = 20) {
  return searchMaster<MasterHsnCode>('/masters/hsn-codes/list', search, pageSize).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeHsn(row as MasterHsnCode)).filter(Boolean) as MasterHsnCode[],
  }))
}

export function searchSacCodes(search: string, pageSize = 20) {
  return searchMaster<MasterSacCode>('/masters/sac-codes/list', search, pageSize).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeSac(row as MasterSacCode)).filter(Boolean) as MasterSacCode[],
  }))
}

export const BUSINESS_PARTNER_DROPDOWN_FIELDS = ['CardCode', 'CardName', 'Series']

export function searchVendors(search: string, pageSize = 20, fields: string[] = BUSINESS_PARTNER_DROPDOWN_FIELDS) {
  return searchMaster<MasterBusinessPartner>('/business-partner/list', search, pageSize, fields).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeBusinessPartner(row as MasterBusinessPartner)).filter(Boolean) as MasterBusinessPartner[],
  }))
}

export function searchSalesPersons(search: string, pageSize = 20) {
  return searchMaster<MasterSalesPerson>('/masters/sales-persons/list', search, pageSize).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => {
      const source = row as Record<string, unknown>
      const code = Number(source.SalesEmployeeCode ?? source.salesEmployeeCode)
      if (!Number.isFinite(code)) return undefined
      return {
        SalesEmployeeCode: code,
        SalesEmployeeName: String(source.SalesEmployeeName ?? source.salesEmployeeName ?? ''),
      } satisfies MasterSalesPerson
    }).filter(Boolean) as MasterSalesPerson[],
  }))
}

export function searchEmployees(search: string, pageSize = 20) {
  return searchMaster<MasterEmployee>('/masters/employees/list', search, pageSize).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeEmployee(row as Record<string, unknown>)).filter(Boolean) as MasterEmployee[],
  }))
}

export async function lookupSalesPerson(salesEmployeeCode: number | string): Promise<MasterSalesPerson | undefined> {
  const code = Number(salesEmployeeCode)
  if (!Number.isFinite(code)) return undefined
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/masters/sales-persons/${code}`)
    const resolved = Number(raw.SalesEmployeeCode ?? raw.salesEmployeeCode)
    if (!Number.isFinite(resolved)) return undefined
    return {
      SalesEmployeeCode: resolved,
      SalesEmployeeName: String(raw.SalesEmployeeName ?? raw.salesEmployeeName ?? ''),
    }
  } catch {
    return undefined
  }
}

export async function lookupEmployee(employeeId: number | string): Promise<MasterEmployee | undefined> {
  const id = Number(employeeId)
  if (!Number.isFinite(id)) return undefined
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/masters/employees/${id}`)
    return normalizeEmployee(raw)
  } catch {
    return undefined
  }
}

export function searchCustomers(search: string, pageSize = 20, fields: string[] = BUSINESS_PARTNER_DROPDOWN_FIELDS) {
  return searchMaster<MasterBusinessPartner>('/business-partner/customers/list', search, pageSize, fields).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeBusinessPartner(row as MasterBusinessPartner)).filter(Boolean) as MasterBusinessPartner[],
  }))
}

/** Vendors and customers merged — for Dispatch To / Ship To on PO logistics. */
export async function searchBusinessPartners(search: string, pageSize = 20) {
  const half = Math.max(10, Math.ceil(pageSize / 2))
  const [vendors, customers] = await Promise.all([
    searchVendors(search, half),
    searchCustomers(search, half),
  ])
  const byCode = new Map<string, MasterBusinessPartner>()
  for (const bp of [...(vendors.data ?? []), ...(customers.data ?? [])]) {
    if (bp.CardCode) byCode.set(bp.CardCode, bp)
  }
  const data = [...byCode.values()].sort((a, b) => (a.CardCode ?? '').localeCompare(b.CardCode ?? ''))
  return {
    ...vendors,
    data: data.slice(0, pageSize),
    totalCount: data.length,
  }
}

/** Sales order rows for lookups. The API selects DocNum, DocEntry, CardCode, CardName, NumAtCard and Project. */
export interface MasterSalesOrder {
  DocNum?: number
  DocEntry?: number
  CardCode?: string
  CardName?: string
  NumAtCard?: string
  Project?: string
}

export function listSalesOrders(search: string, customerId?: string, pageSize = 20) {
  const request: PaginationRequest = createMasterSearchRequest(search, { pageSize })
  const query = customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''
  return apiListPost<MasterSalesOrder>(
    `/masters/sales-orders/list${query}`,
    request,
  ) as Promise<PaginationResponse<MasterSalesOrder[]>>
}

export async function lookupItem(itemCode: string): Promise<MasterItem | undefined> {
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/masters/items/${encodeURIComponent(itemCode.trim())}`)
    return normalizeItem(raw)
  } catch {
    return undefined
  }
}

export async function lookupProject(projectCode: string): Promise<MasterProject | undefined> {
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/masters/projects/${encodeURIComponent(projectCode.trim())}`)
    return normalizeProject(raw)
  } catch {
    return undefined
  }
}

export async function lookupBusinessPartner(cardCode: string): Promise<MasterBusinessPartner | undefined> {
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/business-partner/${encodeURIComponent(cardCode.trim())}`)
    return normalizeBusinessPartner(raw)
  } catch {
    return undefined
  }
}

export interface BusinessPartnerAddressOption {
  addressName: string
  addressType: string
  formattedAddress: string
}

export interface BusinessPartnerContactOption {
  internalCode?: number
  name: string
  position?: string
  phone?: string
}

export interface BusinessPartnerLogisticsDetails {
  cardCode?: string
  cardName?: string
  defaultShipTo?: string
  defaultContactPerson?: string
  addresses: BusinessPartnerAddressOption[]
  contacts: BusinessPartnerContactOption[]
}

/** Addresses + contacts for PO Logistics Dispatch Address / Contact Person. */
export async function fetchBusinessPartnerLogistics(cardCode: string): Promise<BusinessPartnerLogisticsDetails | undefined> {
  const { apiGet } = await import('@/helpers/api/client')
  try {
    const raw = await apiGet<Record<string, unknown>>(`/business-partner/${encodeURIComponent(cardCode.trim())}/logistics`)
    const addressesRaw = (raw.addresses ?? raw.Addresses ?? []) as Array<Record<string, unknown>>
    const contactsRaw = (raw.contacts ?? raw.Contacts ?? []) as Array<Record<string, unknown>>
    return {
      cardCode: raw.cardCode != null ? String(raw.cardCode) : raw.CardCode != null ? String(raw.CardCode) : undefined,
      cardName: raw.cardName != null ? String(raw.cardName) : raw.CardName != null ? String(raw.CardName) : undefined,
      defaultShipTo: raw.defaultShipTo != null ? String(raw.defaultShipTo) : raw.DefaultShipTo != null ? String(raw.DefaultShipTo) : undefined,
      defaultContactPerson: raw.defaultContactPerson != null
        ? String(raw.defaultContactPerson)
        : raw.DefaultContactPerson != null
          ? String(raw.DefaultContactPerson)
          : undefined,
      addresses: addressesRaw.map((a) => ({
        addressName: String(a.addressName ?? a.AddressName ?? ''),
        addressType: String(a.addressType ?? a.AddressType ?? ''),
        formattedAddress: String(a.formattedAddress ?? a.FormattedAddress ?? ''),
      })).filter((a) => a.formattedAddress || a.addressName),
      contacts: contactsRaw.map((c) => ({
        internalCode: c.internalCode != null || c.InternalCode != null
          ? Number(c.internalCode ?? c.InternalCode)
          : undefined,
        name: String(c.name ?? c.Name ?? '').trim(),
        position: c.position != null || c.Position != null ? String(c.position ?? c.Position) : undefined,
        phone: c.phone != null || c.Phone != null ? String(c.phone ?? c.Phone) : undefined,
      })).filter((c) => c.name),
    }
  } catch {
    return undefined
  }
}

export async function batchMasterLookup(payload: MasterLookupPayload): Promise<MasterLookupResult> {
  const { apiPost } = await import('@/helpers/api/client')
  const raw = await apiPost<Record<string, unknown>>('/masters/lookup', {
    ItemCodes: payload.itemCodes ?? [],
    ProjectCodes: payload.projectCodes ?? [],
    CardCodes: payload.cardCodes ?? [],
  })
  return normalizeLookupResult(raw)
}

export interface PaymentTermTypeOption {
  value: string
  description: string
}

/** SAP OPOR T1 ValidValues (+ app extras). Falls back to built-in defaults on failure. */
export async function fetchPaymentTermTypes(): Promise<PaymentTermTypeOption[]> {
  const { apiGet } = await import('@/helpers/api/client')
  const { PAYMENT_TERM_TYPE_OPTIONS } = await import('@/types/purchaseOrder')
  const fallback = PAYMENT_TERM_TYPE_OPTIONS.map((o) => ({
    value: o.value,
    description: o.label,
  }))
  try {
    const raw = await apiGet<Array<Record<string, unknown>> | PaymentTermTypeOption[]>('/masters/payment-term-types')
    const rows = (Array.isArray(raw) ? raw : []).map((row) => {
      const source = row as Record<string, unknown>
      const value = String(source.value ?? source.Value ?? '').trim()
      const description = String(source.description ?? source.Description ?? value).trim()
      if (!value) return undefined
      return { value, description: description || value } satisfies PaymentTermTypeOption
    }).filter(Boolean) as PaymentTermTypeOption[]
    return rows.length > 0 ? rows : fallback
  } catch {
    return fallback
  }
}

export interface PurchaseOrderLogisticsUdfOptions {
  priceBasis: PaymentTermTypeOption[]
  modeOfTransport: PaymentTermTypeOption[]
}

function mapUdfOptions(raw: unknown): PaymentTermTypeOption[] {
  if (!Array.isArray(raw)) return []
  return raw.map((row) => {
    const source = row as Record<string, unknown>
    const value = String(source.value ?? source.Value ?? '').trim()
    const description = String(source.description ?? source.Description ?? value).trim()
    if (!value) return undefined
    return { value, description: description || value } satisfies PaymentTermTypeOption
  }).filter(Boolean) as PaymentTermTypeOption[]
}

/** SAP U_PRI_BAS + U_TransMode ValidValues for PO Logistics dropdowns. */
export async function fetchPurchaseOrderLogisticsOptions(): Promise<PurchaseOrderLogisticsUdfOptions> {
  const { apiGet } = await import('@/helpers/api/client')
  const { PRICE_BASIS_OPTIONS, MODE_OF_TRANSPORT_OPTIONS } = await import('@/types/purchaseOrder')
  const fallback: PurchaseOrderLogisticsUdfOptions = {
    priceBasis: PRICE_BASIS_OPTIONS.map((o) => ({ value: o.value, description: o.label })),
    modeOfTransport: MODE_OF_TRANSPORT_OPTIONS.map((o) => ({ value: o.value, description: o.label })),
  }
  try {
    const raw = await apiGet<Record<string, unknown>>('/masters/purchase-order-logistics-options')
    const priceBasis = mapUdfOptions(raw.priceBasis ?? raw.PriceBasis)
    const modeOfTransport = mapUdfOptions(raw.modeOfTransport ?? raw.ModeOfTransport)
    return {
      priceBasis: priceBasis.length > 0 ? priceBasis : fallback.priceBasis,
      modeOfTransport: modeOfTransport.length > 0 ? modeOfTransport : fallback.modeOfTransport,
    }
  } catch {
    return fallback
  }
}
