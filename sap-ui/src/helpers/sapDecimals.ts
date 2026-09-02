/**
 * SAP Business One Administration → Decimal Places for this company:
 * Amounts 2, Prices 3, Rates 4, Quantities 4, Percent 2, Units 2.
 */
export const SAP_DECIMAL_PLACES = {
  amounts: 2,
  prices: 3,
  rates: 4,
  quantities: 4,
  percent: 2,
  units: 2,
} as const

export type SapDecimalKind = keyof typeof SAP_DECIMAL_PLACES

export function sapDecimalStep(decimals: number): string {
  if (!Number.isFinite(decimals) || decimals <= 0) return '1'
  return `0.${'0'.repeat(decimals - 1)}1`
}

export function formatSapDecimal(
  value: number | undefined | null,
  decimals: number,
  empty = '—',
): string {
  if (value == null || Number.isNaN(Number(value))) return empty
  return Number(value).toLocaleString(undefined, {
    minimumFractionDigits: 0,
    maximumFractionDigits: decimals,
  })
}

export function formatSapFixed(
  value: number | undefined | null,
  decimals: number,
): string {
  return Number(value ?? 0).toLocaleString(undefined, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
}
