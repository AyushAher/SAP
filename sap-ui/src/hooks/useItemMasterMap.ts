import { useEffect, useState } from 'react'
import { resolveItem } from '@/helpers/masterLookup'

export interface ItemMasterDetails {
  name?: string
  uom?: string
}

export function useItemMasterMap(itemCodes: Array<string | undefined | null>) {
  const [itemMap, setItemMap] = useState<Record<string, ItemMasterDetails>>({})

  useEffect(() => {
    const codes = [...new Set(itemCodes.map((code) => code?.trim()).filter(Boolean) as string[])]
    if (!codes.length) {
      setItemMap({})
      return
    }

    let cancelled = false
    void Promise.all(codes.map(async (code) => {
      const item = await resolveItem(code)
      return [code, { name: item?.ItemName, uom: item?.InventoryUom }] as const
    })).then((entries) => {
      if (cancelled) return
      setItemMap(Object.fromEntries(entries))
    })

    return () => {
      cancelled = true
    }
  }, [itemCodes.join('|')])

  return itemMap
}
