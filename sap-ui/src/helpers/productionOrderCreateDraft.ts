import type { ProductionOrder } from '@/types/production'

const STORAGE_KEY = 'sap_po_create_draft'

export interface ProductionOrderCreateDraftLabels {
  customerLabel?: string
  itemLabel?: string
  salesOrderLabel?: string
  projectName?: string
}

export interface ProductionOrderCreateDraft {
  header: ProductionOrder
  labels?: ProductionOrderCreateDraftLabels
  subassemblies: ProductionOrder[]
}

function canUseStorage(): boolean {
  return typeof sessionStorage !== 'undefined'
}

export function loadCreateDraft(): ProductionOrderCreateDraft | null {
  if (!canUseStorage()) return null
  const raw = sessionStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as ProductionOrderCreateDraft
    if (!parsed || typeof parsed !== 'object') return null
    return {
      header: parsed.header ?? {},
      labels: parsed.labels,
      subassemblies: Array.isArray(parsed.subassemblies) ? parsed.subassemblies : [],
    }
  } catch {
    return null
  }
}

export function saveCreateDraft(draft: ProductionOrderCreateDraft): void {
  if (!canUseStorage()) return
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify({
    header: draft.header ?? {},
    labels: draft.labels,
    subassemblies: draft.subassemblies ?? [],
  }))
}

export function saveCreateDraftHeader(
  header: ProductionOrder,
  labels?: ProductionOrderCreateDraftLabels,
): ProductionOrderCreateDraft {
  const current = loadCreateDraft() ?? { header: {}, subassemblies: [] }
  const next: ProductionOrderCreateDraft = {
    header,
    labels: labels ?? current.labels,
    subassemblies: current.subassemblies,
  }
  saveCreateDraft(next)
  return next
}

export function upsertDraftSubassembly(subassembly: ProductionOrder): ProductionOrderCreateDraft {
  const current = loadCreateDraft() ?? { header: {}, subassemblies: [] }
  const key = subassembly.DraftKey
  const index = key
    ? current.subassemblies.findIndex((row) => row.DraftKey === key)
    : -1
  const subassemblies = [...current.subassemblies]
  if (index >= 0) subassemblies[index] = subassembly
  else subassemblies.push(subassembly)
  const next: ProductionOrderCreateDraft = { ...current, subassemblies }
  saveCreateDraft(next)
  return next
}

export function removeDraftSubassembly(draftKey: string): ProductionOrderCreateDraft {
  const current = loadCreateDraft() ?? { header: {}, subassemblies: [] }
  const next: ProductionOrderCreateDraft = {
    ...current,
    subassemblies: current.subassemblies.filter((row) => row.DraftKey !== draftKey),
  }
  saveCreateDraft(next)
  return next
}

export function clearCreateDraft(): void {
  if (!canUseStorage()) return
  sessionStorage.removeItem(STORAGE_KEY)
}

export function newDraftSubassemblyKey(existing: ProductionOrder[] = []): string {
  let max = 0
  for (const row of existing) {
    const match = /^draft-(\d+)$/.exec(row.DraftKey ?? '')
    const n = match ? Number.parseInt(match[1], 10) : 0
    if (n > max) max = n
  }
  return `draft-${max + 1}`
}
