import {
  ITEM_DETAIL_FIELDS,
  searchItems,
  type MasterItem,
} from '@/Requests/masters'
import { rememberMasterItem } from '@/helpers/masterLookup'

const ITEM_LIST_PAGE_SIZE = 500
const REMOTE_FALLBACK_MIN_TERM = 2

let itemList: MasterItem[] | null = null
let itemListPending: Promise<MasterItem[]> | null = null

function remember(items: MasterItem[]): void {
  for (const item of items) rememberMasterItem(item)
}

function mergeIntoList(incoming: MasterItem[]): void {
  const byCode = new Map<string, MasterItem>()
  for (const item of itemList ?? []) {
    const code = item.ItemCode?.trim()
    if (code) byCode.set(code, item)
  }
  for (const item of incoming) {
    const code = item.ItemCode?.trim()
    if (code) byCode.set(code, { ...byCode.get(code), ...item, ItemCode: code })
  }
  itemList = [...byCode.values()]
  remember(incoming)
}

function filterItems(items: MasterItem[], search: string): MasterItem[] {
  const term = search.trim().toLowerCase()
  if (!term) return items
  return items.filter((item) => {
    const code = (item.ItemCode ?? '').toLowerCase()
    const name = (item.ItemName ?? '').toLowerCase()
    return code.includes(term) || name.includes(term)
  })
}

/** One SAP item-list fetch, reused while adding sub-assembly rows. */
export async function ensureItemList(): Promise<MasterItem[]> {
  if (itemList) return itemList
  if (!itemListPending) {
    itemListPending = searchItems('', ITEM_LIST_PAGE_SIZE, ITEM_DETAIL_FIELDS)
      .then((response) => {
        const rows = response.data ?? []
        itemList = rows
        remember(rows)
        return rows
      })
      .catch((error) => {
        itemListPending = null
        throw error
      })
  }
  return itemListPending
}

export async function searchItemsCached(
  search: string,
  pageSize = 20,
): Promise<{ data: MasterItem[] }> {
  const cached = await ensureItemList()
  const term = search.trim()
  let rows = filterItems(cached, term)
  if (term.length >= REMOTE_FALLBACK_MIN_TERM && rows.length === 0) {
    const remote = await searchItems(term, pageSize, ITEM_DETAIL_FIELDS)
    mergeIntoList(remote.data ?? [])
    rows = filterItems(itemList ?? cached, term)
  }
  return { data: rows.slice(0, pageSize) }
}

export function resetItemSearchCache(): void {
  itemList = null
  itemListPending = null
}
