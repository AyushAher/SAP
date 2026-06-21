export const SAP_COMPANY_DATABASES = [
  { value: 'PBBPL_LIVE', label: 'PBBPL Live' },
  { value: 'PBBPL_UAT', label: 'PBBPL UAT' },
] as const

export type SapCompanyDatabase = (typeof SAP_COMPANY_DATABASES)[number]['value']

export const DEFAULT_COMPANY_DB: SapCompanyDatabase = 'PBBPL_UAT'
