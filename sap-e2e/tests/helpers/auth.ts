import { type Locator, type Page, expect } from '@playwright/test'

export const E2E_USER = process.env.E2E_USERNAME ?? 'manager'
export const E2E_PASSWORD = process.env.E2E_PASSWORD ?? '1946'
export const E2E_COMPANY_DB = process.env.E2E_COMPANY_DB ?? 'PBBPL_UAT'

export async function loginAsManager(page: Page) {
  await page.goto('/auth/login')
  await expect(page.getByRole('heading', { name: /welcome back/i })).toBeVisible()

  const company = page.getByRole('combobox', { name: 'SAP Company Database' })
  if (await company.isVisible()) {
    const current = (await company.textContent()) ?? ''
    if (!current.includes(E2E_COMPANY_DB) && !current.toLowerCase().includes('uat')) {
      await company.click()
      await page.getByRole('option', { name: /PBBPL UAT/i }).click()
    }
  }

  await page.getByLabel('Username').fill(E2E_USER)
  await page.getByLabel('Password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: /sign in/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/auth/'), { timeout: 30_000 })
}

export async function openPurchaseRequestForm(page: Page) {
  await page.goto('/purchase-requests')
  await expect(page.getByRole('heading', { name: 'Purchase Requests' })).toBeVisible()
  await page.getByTestId('purchase-request-add').click()
  await expect(page.getByTestId('purchase-request-form')).toBeVisible()
}

export async function chooseSearchableOption(
  page: Page,
  comboboxName: string,
  search: string,
  scope?: Locator,
) {
  const host = scope ?? page
  await host.getByRole('combobox', { name: comboboxName }).click()
  const searchBox = page.getByPlaceholder('Type to search...')
  await expect(searchBox).toBeVisible()
  await searchBox.fill(search)
  const option = page.getByRole('option').filter({ hasText: search }).first()
  await expect(option).toBeVisible({ timeout: 20_000 })
  await option.click()
}

export function todayDdMmYyyy(date = new Date()) {
  const dd = String(date.getDate()).padStart(2, '0')
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const yyyy = date.getFullYear()
  return `${dd}/${mm}/${yyyy}`
}

export function addDaysDdMmYyyy(days: number, from = new Date()) {
  const date = new Date(from)
  date.setDate(date.getDate() + days)
  return todayDdMmYyyy(date)
}
