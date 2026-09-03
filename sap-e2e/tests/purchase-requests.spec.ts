import { test, expect, type Page } from '@playwright/test'
import {
  addDaysDdMmYyyy,
  chooseSearchableOption,
  loginAsManager,
  openPurchaseRequestForm,
  todayDdMmYyyy,
} from './helpers/auth'

const SAMPLE_ITEM = process.env.E2E_PR_ITEM ?? 'CO3523639606300000'
const SAMPLE_WAREHOUSE = process.env.E2E_PR_WAREHOUSE ?? 'Store1'

test.describe.configure({ mode: 'serial' })

test.describe('Purchase Requests', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsManager(page)
  })

  test('list page shows sync and add actions', async ({ page }) => {
    await page.goto('/purchase-requests')
    await expect(page.getByRole('heading', { name: 'Purchase Requests' })).toBeVisible()
    await expect(page.getByTestId('purchase-request-sync')).toBeVisible()
    await expect(page.getByTestId('purchase-request-add')).toBeVisible()
  })

  test('create validation requires at least one line', async ({ page }) => {
    await openPurchaseRequestForm(page)
    await fillRequiredDate(page, todayDdMmYyyy())
    await page.getByTestId('purchase-request-submit').click()
    await expect(page.getByRole('status').filter({ hasText: /add at least one line item/i })).toBeVisible()
  })

  test('create, update header, add/edit/delete lines, then cancel', async ({ page }) => {
    test.setTimeout(240_000)
    await openPurchaseRequestForm(page)

    await fillRequiredDate(page, todayDdMmYyyy())
    await page.getByTestId('purchase-request-requester').fill('manager')
    await addPaymentTerm(page, '100')
    await addItemLine(page, SAMPLE_ITEM, SAMPLE_WAREHOUSE, '1')
    await page.getByTestId('purchase-request-submit').click()
    await expectSaved(page)
    await expect(page).toHaveURL(/\/purchase-requests$/)

    const docEntry = await latestDocEntryFromList(page)
    await openPurchaseRequestByDocEntry(page, docEntry)

    const nextRequired = addDaysDdMmYyyy(1)
    await fillRequiredDate(page, nextRequired)
    await page.getByTestId('purchase-request-submit').click()
    await expectSaved(page)

    await openPurchaseRequestByDocEntry(page, docEntry)
    await expect(page.getByLabel('Required Date')).toHaveValue(nextRequired)

    await addItemLine(page, SAMPLE_ITEM, SAMPLE_WAREHOUSE, '2')
    await page.getByTestId('purchase-request-submit').click()
    await expectSaved(page)

    await openPurchaseRequestByDocEntry(page, docEntry)
    await expect(page.getByRole('row').filter({ hasText: SAMPLE_ITEM })).toHaveCount(2)

    await page.getByTitle('Edit item').nth(1).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await dialog.getByLabel('Purchase Qty *').fill('3')
    await page.getByTestId('document-line-save').click()
    await expect(dialog).toBeHidden()
    await page.getByTestId('purchase-request-submit').click()
    await expectSaved(page)

    await openPurchaseRequestByDocEntry(page, docEntry)
    await page.getByTitle('Delete item').last().click()
    await expect(page.getByRole('row').filter({ hasText: SAMPLE_ITEM })).toHaveCount(1)
    await page.getByTestId('purchase-request-submit').click()
    await expectSaved(page)

    await openPurchaseRequestByDocEntry(page, docEntry)
    page.once('dialog', (d) => d.accept())
    await page.getByTestId('purchase-request-cancel-document').click()
    await expect(page.getByRole('status').filter({ hasText: /cancelled/i })).toBeVisible({ timeout: 60_000 })
  })
})

async function fillRequiredDate(page: Page, value: string) {
  const requiredDate = page.getByLabel('Required Date')
  await requiredDate.fill(value)
  await requiredDate.blur()
  await expect(requiredDate).toHaveValue(value)
}

async function addPaymentTerm(page: Page, percent: string) {
  await page.getByRole('tab', { name: /Payment Terms/i }).click()
  const panel = page.getByRole('tabpanel')
  await panel.getByLabel('Payment %').fill(percent)
  await page.getByTestId('purchase-request-add-payment-term').click()
  await expect(page.getByText('No payment terms added.')).toHaveCount(0)
  await page.getByRole('tab', { name: /^Items$/i }).click()
}

async function expectSaved(page: Page) {
  const toast = page.getByRole('status').filter({ hasText: /purchase request .* saved/i })
  await expect(toast).toBeVisible({ timeout: 90_000 })
}

async function latestDocEntryFromList(page: Page) {
  await page.goto('/purchase-requests')
  await expect(page.getByRole('heading', { name: 'Purchase Requests' })).toBeVisible()
  const cell = page.locator('table tbody tr').first().locator('td').first()
  await expect(cell).toBeVisible({ timeout: 30_000 })
  const docEntry = (await cell.innerText()).trim()
  expect(docEntry, 'expected latest list row to have a Doc Entry').toMatch(/^\d+$/)
  return docEntry
}

async function openPurchaseRequestByDocEntry(page: Page, docEntry: string) {
  await page.goto('/purchase-requests')
  await expect(page.getByRole('heading', { name: 'Purchase Requests' })).toBeVisible()
  const row = page.getByRole('row').filter({
    has: page.getByRole('cell', { name: docEntry, exact: true }),
  }).first()
  await expect(row).toBeVisible({ timeout: 30_000 })
  await row.getByRole('button', { name: 'Actions' }).click()
  await page.getByRole('menuitem', { name: 'Edit' }).click()
  await expect(page.getByTestId('purchase-request-form')).toBeVisible()
  await expect(page.getByRole('status', { name: 'Loading purchase request...' })).toHaveCount(0)
  await expect(page.getByTestId('purchase-request-requester')).not.toHaveValue('')
}

async function addItemLine(
  page: Page,
  itemCode: string,
  warehouse: string,
  qty: string,
) {
  await page.getByTestId('document-line-add').click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toBeVisible()
  await chooseSearchableOption(page, 'Item', itemCode, dialog)
  await chooseSearchableOption(page, 'Warehouse', warehouse, dialog)
  await dialog.getByLabel('Purchase Qty *').fill(qty)
  await page.getByTestId('document-line-save').click()
  await expect(dialog).toBeHidden()
  await expect(page.getByRole('row').filter({ hasText: itemCode }).first()).toBeVisible()
}
