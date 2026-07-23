import { apiListPost } from '@/helpers/api/list'
import { createMasterSearchRequest } from '@/helpers/api/masterSearch'
import type { PaginationRequest, PaginationResponse } from '@/types/api'

export interface MasterItem {
  ItemCode?: string
  ItemName?: string
  InventoryUom?: string
  InventoryWeight?: number
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

export interface MasterBusinessPartner {
  CardCode?: string
  CardName?: string
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
    InventoryWeight: Number(source.InventoryWeight ?? source.inventoryWeight ?? 0) || undefined,
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
  return {
    CardCode: String(cardCode),
    CardName: String(source.CardName ?? source.cardName ?? ''),
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

/** Fields needed just to render a code+name dropdown option for an item. */
export const ITEM_DROPDOWN_FIELDS = ['ItemCode', 'ItemName']
/**
 * Fields needed by line editors that also resolve UOM for the selected item. Note: `InventoryWeight`
 * is intentionally not part of the items *list* endpoint's field set (see `SapPaginationProfiles.Items`)
 * — it's only available via the by-code lookup (`lookupItem`), so it's omitted here.
 */
export const ITEM_DETAIL_FIELDS = ['ItemCode', 'ItemName', 'InventoryUOM']
export const WAREHOUSE_DROPDOWN_FIELDS = ['WarehouseCode', 'City']
export const TAX_CODE_DROPDOWN_FIELDS = ['Code', 'Name', 'Rate']
export const PROJECT_DROPDOWN_FIELDS = ['Code', 'Name']
export const BUSINESS_PARTNER_DROPDOWN_FIELDS = ['CardCode', 'CardName']

export function searchItems(search: string, pageSize = 20, fields: string[] = ITEM_DROPDOWN_FIELDS) {
  return searchMaster<MasterItem>('/masters/items/list', search, pageSize, fields)
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

export function searchVendors(search: string, pageSize = 20, fields: string[] = BUSINESS_PARTNER_DROPDOWN_FIELDS) {
  return searchMaster<MasterBusinessPartner>('/business-partner/list', search, pageSize, fields)
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
