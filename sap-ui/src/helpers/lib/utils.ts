import { clsx, type ClassValue } from 'clsx'

export function cn(...inputs: ClassValue[]) {
  return clsx(inputs)
}

export function formatDate(date: Date | string): string {
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(date))
}

/** Purchase-order display dates — dd/MM/yyyy (Indian DD/MM/YYYY). */
export function formatPoDisplayDate(value?: string | Date | null): string {
  const iso = toIsoDateOnly(value)
  if (!iso) return ''
  const [y, m, d] = iso.split('-')
  return `${d}/${m}/${y}`
}

/** Parse dd/MM/yyyy or ddMMyyyy into ISO yyyy-MM-dd. */
export function parsePoDisplayDate(value: string): string | undefined {
  const trimmed = value.trim()
  if (!trimmed) return undefined
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed
  const slash = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/)
  if (slash) {
    const day = slash[1].padStart(2, '0')
    const month = slash[2].padStart(2, '0')
    const year = slash[3]
    return `${year}-${month}-${day}`
  }
  const compact = trimmed.match(/^(\d{2})(\d{2})(\d{4})$/)
  if (compact) {
    return `${compact[3]}-${compact[2]}-${compact[1]}`
  }
  return undefined
}

export function toIsoDateOnly(value?: string | Date | null): string | undefined {
  if (!value) return undefined
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) return undefined
    return value.toISOString().slice(0, 10)
  }
  const trimmed = value.trim()
  if (!trimmed) return undefined
  if (/^\d{4}-\d{2}-\d{2}/.test(trimmed)) return trimmed.slice(0, 10)
  return parsePoDisplayDate(trimmed)
}
