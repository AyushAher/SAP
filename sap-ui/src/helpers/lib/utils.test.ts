import { describe, expect, it } from 'vitest'
import {
  formatDate,
  formatDateTime,
  formatDisplayDate,
  formatPoDisplayDate,
  toIsoDateOnly,
} from './utils'
import { buildLocalDayRangeFilters } from '../api/pagination'

describe('IST date display', () => {
  it('keeps date-only strings as calendar dates', () => {
    expect(formatPoDisplayDate('2026-08-17')).toBe('17/08/2026')
    expect(formatDate('2026-08-17')).toBe('17/08/2026')
    expect(formatDisplayDate('2026-08-17')).toBe('17/08/2026')
    expect(toIsoDateOnly('2026-08-17')).toBe('2026-08-17')
  })

  it('converts UTC midnight to the IST calendar date', () => {
    expect(toIsoDateOnly('2026-08-17T00:00:00Z')).toBe('2026-08-17')
    expect(formatPoDisplayDate('2026-08-17T00:00:00Z')).toBe('17/08/2026')
  })

  it('converts UTC evening instants to the next IST date', () => {
    expect(toIsoDateOnly('2026-08-16T18:30:00.000Z')).toBe('2026-08-17')
    expect(formatPoDisplayDate('2026-08-16T18:30:00.000Z')).toBe('17/08/2026')
    expect(formatDate('2026-08-16T18:30:00.000Z')).toBe('17/08/2026')
  })

  it('formats UTC timestamps as IST date and time', () => {
    expect(formatDateTime('2024-06-01T10:00:00Z')).toBe('01/06/2024, 03:30:00 PM')
    expect(formatDisplayDate('2024-06-01T10:00:00Z')).toBe('01/06/2024, 03:30:00 PM')
  })
})

describe('buildLocalDayRangeFilters', () => {
  it('uses the IST day bounds in UTC', () => {
    const filters = buildLocalDayRangeFilters('createdAt', '2026-08-25')
    expect(filters).toEqual([
      { field: 'createdAt', operator: 'gte', value: '2026-08-24T18:30:00.000Z' },
      { field: 'createdAt', operator: 'lt', value: '2026-08-25T18:30:00.000Z' },
    ])
  })
})
