import { clsx, type ClassValue } from 'clsx'

export function cn(...inputs: ClassValue[]) {
  return clsx(inputs)
}

/** All UI dates are shown in India Standard Time. */
export const DISPLAY_TIME_ZONE = 'Asia/Kolkata'

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/
const HAS_TIME = /T\d{2}:\d{2}/

function part(parts: Intl.DateTimeFormatPart[], type: Intl.DateTimeFormatPartTypes): string {
  return parts.find((p) => p.type === type)?.value ?? ''
}

function istParts(date: Date): Intl.DateTimeFormatPart[] {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: DISPLAY_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).formatToParts(date)
}

function parseInstant(value: Date | string): Date | undefined {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return undefined
  return date
}

/** True when the string carries a time (UTC ISO, offset, or local datetime). */
export function isDateTimeValue(value: string): boolean {
  const trimmed = value.trim()
  return HAS_TIME.test(trimmed) || /Z$/i.test(trimmed) || /[+-]\d{2}:\d{2}$/.test(trimmed)
}

/** Today's calendar date in IST as yyyy-MM-dd. */
export function todayIsoDate(): string {
  return toIstIsoDate(new Date())
}

export function toIstIsoDate(date: Date): string {
  const parts = istParts(date)
  return `${part(parts, 'year')}-${part(parts, 'month')}-${part(parts, 'day')}`
}

/** Date-only display in IST — dd/MM/yyyy. Date-only strings are not shifted. */
export function formatDate(date: Date | string): string {
  if (typeof date === 'string' && DATE_ONLY.test(date.trim())) {
    return formatPoDisplayDate(date)
  }
  const instant = parseInstant(date)
  if (!instant) return ''
  const parts = istParts(instant)
  return `${part(parts, 'day')}/${part(parts, 'month')}/${part(parts, 'year')}`
}

/** UTC/ISO timestamps shown as IST date and time. */
export function formatDateTime(date: Date | string): string {
  const instant = parseInstant(date)
  if (!instant) return ''
  const parts = istParts(instant)
  let hour24 = Number(part(parts, 'hour'))
  if (hour24 === 24) hour24 = 0
  const hour12 = hour24 % 12 || 12
  const ampm = hour24 < 12 ? 'AM' : 'PM'
  const hh = String(hour12).padStart(2, '0')
  return `${part(parts, 'day')}/${part(parts, 'month')}/${part(parts, 'year')}, ${hh}:${part(parts, 'minute')}:${part(parts, 'second')} ${ampm}`
}

/**
 * Display helper: date-only stays dd/MM/yyyy; UTC/datetime values convert to IST
 * and include the time.
 */
export function formatDisplayDate(value?: Date | string | null): string {
  if (value == null || value === '') return ''
  if (typeof value === 'string') {
    const trimmed = value.trim()
    if (!trimmed) return ''
    if (DATE_ONLY.test(trimmed)) return formatPoDisplayDate(trimmed)
    if (isDateTimeValue(trimmed)) return formatDateTime(trimmed)
  }
  const instant = parseInstant(value)
  if (!instant) return typeof value === 'string' ? value : ''
  return formatDateTime(instant)
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
    return toIstIsoDate(value)
  }
  const trimmed = value.trim()
  if (!trimmed) return undefined
  if (DATE_ONLY.test(trimmed)) return trimmed
  if (isDateTimeValue(trimmed)) {
    const instant = parseInstant(trimmed)
    return instant ? toIstIsoDate(instant) : undefined
  }
  if (/^\d{4}-\d{2}-\d{2}/.test(trimmed)) return trimmed.slice(0, 10)
  return parsePoDisplayDate(trimmed)
}
