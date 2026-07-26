import { apiListPost } from '@/helpers/api/list'
import { createMasterSearchRequest } from '@/helpers/api/masterSearch'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface MasterItem {
  ItemCode?: string
  ItemName?: string
  InventoryUom?: string
  PurchaseUnit?: string
  InventoryWeight?: number
  ChapterID?: string
  PurchaseVatGroup?: string
}

export interface MasterWarehouse {
  WarehouseCode?: string
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
    InventoryWeight: Number(source.InventoryWeight ?? source.inventoryWeight ?? 0) || undefined,
    ChapterID: source.ChapterID != null || source.chapterID != null
      ? String(source.ChapterID ?? source.chapterID)
      : undefined,
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

async function searchMaster<T>(url: string, search: string, pageSize = 20, fields?: string[]) {
  return apiListPost<T>(url, createMasterSearchRequest(search, { pageSize, fields }))
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
  'InventoryWeight',
  'PurchaseVATGroup',
  'ChapterID',
]
export const WAREHOUSE_DROPDOWN_FIELDS = ['WarehouseCode', 'City']
export const TAX_CODE_DROPDOWN_FIELDS = ['Code', 'Name', 'Rate']
export const PROJECT_DROPDOWN_FIELDS = ['Code', 'Name']
export const GL_ACCOUNT_DROPDOWN_FIELDS = ['Code', 'Name']

export function searchItems(search: string, pageSize = 20, fields: string[] = ITEM_DROPDOWN_FIELDS) {
  return searchMaster<MasterItem>('/masters/items/list', search, pageSize, fields).then((res) => ({
    ...res,
    data: (res.data ?? []).map((row) => normalizeItem(row as MasterItem)).filter(Boolean) as MasterItem[],
  }))
}

export function searchWarehouses(search: string, pageSize = 20, fields: string[] = WAREHOUSE_DROPDOWN_FIELDS) {
  return searchMaster<MasterWarehouse>('/masters/warehouses/list', search, pageSize, fields)
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
    data: (res.data ?? []).map((row) => {
      const source = row as Record<string, unknown>
      const id = Number(source.EmployeeID ?? source.employeeID)
      if (!Number.isFinite(id)) return undefined
      const first = String(source.FirstName ?? source.firstName ?? '')
      const last = String(source.LastName ?? source.lastName ?? '')
      const name = [first, last].filter(Boolean).join(' ')
      return {
        EmployeeID: id,
        FirstName: first || undefined,
        LastName: last || undefined,
        DisplayName: name || String(id),
      } satisfies MasterEmployee
    }).filter(Boolean) as MasterEmployee[],
  }))
}

export function searchCustomers(search: string, pageSize = 20, fields: string[] = BUSINESS_PARTNER_DROPDOWN_FIELDS) {
  return searchMaster<MasterBusinessPartner>('/business-partner/customers/list', search, pageSize, fields)
}

export function listSalesOrders(search: string, customerId?: string, pageSize = 20) {
  const request: PaginationRequest = createMasterSearchRequest(search, { pageSize })
  const query = customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''
  return apiListPost<{ DocNum?: number; DocEntry?: number; CardName?: string; NumAtCard?: string }>(
    `/masters/sales-orders/list${query}`,
    request,
  ) as Promise<PaginationResponse<{ DocNum?: number; DocEntry?: number; CardName?: string; NumAtCard?: string }[]>>
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

export async function batchMasterLookup(payload: MasterLookupPayload): Promise<MasterLookupResult> {
  const { apiPost } = await import('@/helpers/api/client')
  const raw = await apiPost<Record<string, unknown>>('/masters/lookup', {
    ItemCodes: payload.itemCodes ?? [],
    ProjectCodes: payload.projectCodes ?? [],
    CardCodes: payload.cardCodes ?? [],
  })
  return normalizeLookupResult(raw)
}
