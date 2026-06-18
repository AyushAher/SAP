import {
  batchMasterLookup,
  lookupBusinessPartner,
  lookupItem,
  lookupProject,
  type MasterBusinessPartner,
  type MasterItem,
  type MasterProject,
} from '@/Requests/masters'

export interface MasterLookupMaps {
  items: Record<string, string | undefined>
  projects: Record<string, string | undefined>
  businessPartners: Record<string, string | undefined>
}

const emptyMaps = (): MasterLookupMaps => ({
  items: {},
  projects: {},
  businessPartners: {},
})

const itemCache = new Map<string, MasterItem>()
const projectCache = new Map<string, MasterProject>()
const businessPartnerCache = new Map<string, MasterBusinessPartner>()
const pendingItems = new Map<string, Promise<MasterItem | undefined>>()
const pendingProjects = new Map<string, Promise<MasterProject | undefined>>()
const pendingBusinessPartners = new Map<string, Promise<MasterBusinessPartner | undefined>>()

function uniqueCodes(codes: Array<string | undefined | null>): string[] {
  return [...new Set(codes.map((code) => code?.trim()).filter(Boolean) as string[])]
}

export function formatCodeWithName(code?: string | number | null, name?: string | null): string {
  const codeText = code != null && code !== '' ? String(code) : ''
  const nameText = name?.trim()
  if (!codeText) return '—'
  if (!nameText || nameText === codeText) return codeText
  return `${codeText} - ${nameText}`
}

async function runCachedLookup<T>(
  code: string,
  cache: Map<string, T>,
  pending: Map<string, Promise<T | undefined>>,
  loader: (value: string) => Promise<T | undefined>,
): Promise<T | undefined> {
  const trimmed = code.trim()
  if (!trimmed) return undefined
  if (cache.has(trimmed)) return cache.get(trimmed)

  const existing = pending.get(trimmed)
  if (existing) return existing

  const promise = loader(trimmed).then((result) => {
    if (result) cache.set(trimmed, result)
    return result
  }).finally(() => {
    pending.delete(trimmed)
  })

  pending.set(trimmed, promise)
  return promise
}

export async function resolveItem(code: string): Promise<MasterItem | undefined> {
  return runCachedLookup(code, itemCache, pendingItems, lookupItem)
}

export async function resolveProject(code: string): Promise<MasterProject | undefined> {
  return runCachedLookup(code, projectCache, pendingProjects, lookupProject)
}

export async function resolveBusinessPartner(code: string): Promise<MasterBusinessPartner | undefined> {
  return runCachedLookup(code, businessPartnerCache, pendingBusinessPartners, lookupBusinessPartner)
}

export async function resolveItemsMap(codes: string[]): Promise<Record<string, string | undefined>> {
  const unique = uniqueCodes(codes)
  if (!unique.length) return {}

  const batch = await batchMasterLookup({ itemCodes: unique })
  for (const [code, name] of Object.entries(batch.items)) {
    if (name) itemCache.set(code, { ItemCode: code, ItemName: name })
  }

  const map: Record<string, string | undefined> = {}
  for (const code of unique) {
    map[code] = batch.items[code] ?? itemCache.get(code)?.ItemName
  }
  return map
}

export async function resolveProjectsMap(codes: string[]): Promise<Record<string, string | undefined>> {
  const unique = uniqueCodes(codes)
  if (!unique.length) return {}

  const batch = await batchMasterLookup({ projectCodes: unique })
  for (const [code, name] of Object.entries(batch.projects)) {
    if (name) projectCache.set(code, { Code: code, Name: name })
  }

  const map: Record<string, string | undefined> = {}
  for (const code of unique) {
    map[code] = batch.projects[code] ?? projectCache.get(code)?.Name
  }
  return map
}

export async function resolveBusinessPartnersMap(codes: string[]): Promise<Record<string, string | undefined>> {
  const unique = uniqueCodes(codes)
  if (!unique.length) return {}

  const batch = await batchMasterLookup({ cardCodes: unique })
  for (const [code, name] of Object.entries(batch.businessPartners)) {
    if (name) businessPartnerCache.set(code, { CardCode: code, CardName: name })
  }

  const map: Record<string, string | undefined> = {}
  for (const code of unique) {
    map[code] = batch.businessPartners[code] ?? businessPartnerCache.get(code)?.CardName
  }
  return map
}

export async function buildMasterLookupMaps(codes: {
  itemCodes?: string[]
  projectCodes?: string[]
  cardCodes?: string[]
}): Promise<MasterLookupMaps> {
  const itemCodes = uniqueCodes(codes.itemCodes ?? [])
  const projectCodes = uniqueCodes(codes.projectCodes ?? [])
  const cardCodes = uniqueCodes(codes.cardCodes ?? [])

  if (!itemCodes.length && !projectCodes.length && !cardCodes.length) {
    return emptyMaps()
  }

  const batch = await batchMasterLookup({ itemCodes, projectCodes, cardCodes })

  for (const [code, name] of Object.entries(batch.items)) {
    if (name) itemCache.set(code, { ItemCode: code, ItemName: name })
  }
  for (const [code, name] of Object.entries(batch.projects)) {
    if (name) projectCache.set(code, { Code: code, Name: name })
  }
  for (const [code, name] of Object.entries(batch.businessPartners)) {
    if (name) businessPartnerCache.set(code, { CardCode: code, CardName: name })
  }

  return {
    items: batch.items,
    projects: batch.projects,
    businessPartners: batch.businessPartners,
  }
}

export async function resolveMasterSelectLabels(input: {
  itemCode?: string
  projectCode?: string
  customerCode?: string
  vendorCode?: string
}): Promise<{
  itemLabel?: string
  projectLabel?: string
  customerLabel?: string
  vendorLabel?: string
}> {
  const cardCode = input.customerCode ?? input.vendorCode
  const [item, project, partner] = await Promise.all([
    input.itemCode ? resolveItem(input.itemCode) : Promise.resolve(undefined),
    input.projectCode ? resolveProject(input.projectCode) : Promise.resolve(undefined),
    cardCode ? resolveBusinessPartner(cardCode) : Promise.resolve(undefined),
  ])

  return {
    itemLabel: input.itemCode ? formatCodeWithName(input.itemCode, item?.ItemName) : undefined,
    projectLabel: input.projectCode ? formatCodeWithName(input.projectCode, project?.Name) : undefined,
    customerLabel: input.customerCode ? formatCodeWithName(input.customerCode, partner?.CardName) : undefined,
    vendorLabel: input.vendorCode ? formatCodeWithName(input.vendorCode, partner?.CardName) : undefined,
  }
}

export function collectMasterCodes<T>(
  rows: T[],
  extractors: {
    itemCodes?: (row: T) => string | undefined
    projectCodes?: (row: T) => string | undefined
    cardCodes?: (row: T) => string | undefined
  },
) {
  return {
    itemCodes: extractors.itemCodes ? uniqueCodes(rows.map(extractors.itemCodes)) : [],
    projectCodes: extractors.projectCodes ? uniqueCodes(rows.map(extractors.projectCodes)) : [],
    cardCodes: extractors.cardCodes ? uniqueCodes(rows.map(extractors.cardCodes)) : [],
  }
}

export async function buildMasterLookupMapsFromRows<T>(
  rows: T[],
  extractors: {
    itemCodes?: (row: T) => string | undefined
    projectCodes?: (row: T) => string | undefined
    cardCodes?: (row: T) => string | undefined
  },
): Promise<MasterLookupMaps> {
  if (!rows.length) return emptyMaps()
  return buildMasterLookupMaps(collectMasterCodes(rows, extractors))
}

export type MasterLookupKind = 'item' | 'project' | 'businessPartner'

export async function resolveSelectOptionByCode(
  code: string,
  kind: MasterLookupKind,
): Promise<{ value: string; label: string } | undefined> {
  if (kind === 'item') {
    const item = await resolveItem(code)
    return item?.ItemCode ? { value: item.ItemCode, label: formatCodeWithName(item.ItemCode, item.ItemName) } : undefined
  }
  if (kind === 'project') {
    const project = await resolveProject(code)
    return project?.Code ? { value: project.Code, label: formatCodeWithName(project.Code, project.Name) } : undefined
  }
  const partner = await resolveBusinessPartner(code)
  return partner?.CardCode
    ? { value: partner.CardCode, label: formatCodeWithName(partner.CardCode, partner.CardName) }
    : undefined
}
