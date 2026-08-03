import type { MasterHsnCode } from '@/Requests/masters'

/**
 * Resolve PO line HSNEntry from Items.ChapterID.
 * On India localization Items, ChapterID is typically the HSN AbsEntry (numeric).
 * IndiaHsn master ChapterID is the tariff code string (e.g. "73.07.99").
 */
export function pickHsnFromChapterId(
  chapterId: string | undefined,
  rows: MasterHsnCode[],
): { HSNEntry?: number; HsnLabel?: string } | null {
  const code = (chapterId ?? '').trim()
  if (!code) return null

  const asAbs = Number(code)
  const isAbsEntry = Number.isInteger(asAbs) && Number.isFinite(asAbs) && !code.includes('.')

  const exact = isAbsEntry
    ? rows.find((h) => h.AbsEntry === asAbs)
      ?? rows.find((h) => (h.ChapterID ?? '').trim() === code)
    : rows.find((h) => (h.ChapterID ?? '').trim() === code)
      ?? rows.find((h) => (h.DisplayLabel ?? '').includes(code))
      ?? rows[0]

  if (exact?.AbsEntry != null) {
    return {
      HSNEntry: exact.AbsEntry,
      HsnLabel: exact.DisplayLabel ?? String(exact.AbsEntry),
    }
  }

  if (isAbsEntry) {
    return { HSNEntry: asAbs, HsnLabel: code }
  }
  return null
}
