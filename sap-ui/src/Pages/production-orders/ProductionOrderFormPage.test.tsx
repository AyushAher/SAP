import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as apiClient from '@/helpers/api/client'
import { ProductionOrderFormPage } from './ProductionOrderFormPage'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDownloadGet: vi.fn(),
  invalidateCachedGets: vi.fn(),
}))

vi.mock('@/helpers/api/list', () => ({
  apiListPost: vi.fn().mockResolvedValue({ data: [], totalCount: 0 }),
}))

// Master-data name resolution is not what these tests are about, and stubbing it keeps them off
// the network entirely.
vi.mock('@/helpers/masterLookup', () => ({
  formatCodeWithName: (code?: string | number | null, name?: string | null) =>
    [code, name].filter(Boolean).join(' - ') || '—',
  resolveMasterSelectLabels: vi.fn().mockResolvedValue({}),
  resolveProject: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/hooks/useItemMasterMap', () => ({ useItemMasterMap: () => ({}) }))

const apiGet = vi.mocked(apiClient.apiGet)
const apiPost = vi.mocked(apiClient.apiPost)
const apiPut = vi.mocked(apiClient.apiPut)

/** As the API returns it: SAP names, no friendly aliases. */
const sapOrder = {
  AbsoluteEntry: 646,
  DocumentNumber: 10,
  ItemNo: 'FG-001',
  ProductDescription: 'Finished pump',
  ProductionOrderStatus: 'boposPlanned',
  ProductionOrderType: 'bopotSpecial',
  U_ProdType: 'INT',
  U_DwgNo: 'DWG-42',
  U_PrjName: 'Refinery upgrade',
  CustomerCode: 'C000017',
  Project: 'PB-1',
  Warehouse: 'WIP',
  PlannedQuantity: 12,
  CompletedQuantity: 3,
  PostingDate: '2026-06-16T00:00:00Z',
  DueDate: '2026-06-27T00:00:00Z',
  StartDate: '2026-06-18T00:00:00Z',
  ProductionOrderOriginNumber: 252610128,
  ProductionOrderOriginEntry: 156,
  ProductionOrderLines: [
    { LineNumber: 0, ItemNo: 'RM-100', ItemName: 'Steel plate', PlannedQuantity: 24, Warehouse: 'Store1', UoMCode: 6 },
  ],
}

function renderEditForm() {
  return render(
    <MemoryRouter initialEntries={['/production-orders/form/646']}>
      <Routes>
        <Route path="/production-orders/form/:id" element={<ProductionOrderFormPage />} />
        <Route path="/production-orders" element={<div>Production order list</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ProductionOrderFormPage', () => {
  beforeEach(() => {
    // The dropdown keeps the highlighted option in view; jsdom has no such method.
    Element.prototype.scrollIntoView = vi.fn()
    vi.clearAllMocks()
    apiPost.mockResolvedValue({})
    apiPut.mockResolvedValue({ AbsoluteEntry: 646, DocumentNumber: 10 })
    apiGet.mockImplementation(async (url: string) => (url === '/production-orders/646' ? sapOrder : {}) as never)
  })

  it('sends the status the user picked, under the name the API binds', async () => {
    const user = userEvent.setup()
    renderEditForm()

    await screen.findByRole('combobox', { name: /Status/i })
    await user.click(screen.getByRole('combobox', { name: /Status/i }))
    await user.click(screen.getByRole('option', { name: 'Released' }))
    await user.click(screen.getByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    const body = JSON.parse(JSON.stringify(apiPut.mock.calls[0][1])) as Record<string, unknown>
    expect(body.ProductionOrderStatus).toBe('boposReleased')
    expect(body.ItemNo).toBe('FG-001')
    expect(body.U_ProdType).toBe('INT')
    expect(body.U_DwgNo).toBe('DWG-42')
    expect(body.ProductionOrderOriginNumber).toBe(252610128)
    expect(body.ProductionOrderOriginEntry).toBe(156)
    expect(body.DueDate).toBe('2026-06-27')
    expect(body).not.toHaveProperty('Status')
  })

  it('sends the production category the user picked together with the warehouses it implies', async () => {
    const user = userEvent.setup()
    renderEditForm()

    await screen.findByRole('combobox', { name: /Production Category/i })
    await user.click(screen.getByRole('combobox', { name: /Production Category/i }))
    await user.click(screen.getByRole('option', { name: /^JOB/ }))
    await user.click(screen.getByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    const body = JSON.parse(JSON.stringify(apiPut.mock.calls[0][1])) as Record<string, unknown>
    expect(body.U_ProdType).toBe('JOB')
    expect(body.Warehouse).toBe('Subcon')
    expect((body.ProductionOrderLines as Array<Record<string, unknown>>)[0].Warehouse).toBe('Store1')
  })

  it('warns that the component lines need reviewing when the header quantity changes', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const quantity = await screen.findByLabelText(/^Planned Qty/)
    await user.clear(quantity)
    await user.type(quantity, '20')

    expect(await screen.findByRole('status')).toHaveTextContent(/review them before saving/i)
    // The line quantity is left exactly as it was.
    const lineQuantities = screen.getAllByRole('spinbutton').map((input) => (input as HTMLInputElement).value)
    expect(lineQuantities).toContain('24')
  })

  it('refuses to save once the last component line is removed', async () => {
    const user = userEvent.setup()
    renderEditForm()

    await user.click((await screen.findAllByRole('checkbox'))[0])
    await user.click(screen.getByRole('button', { name: 'Remove Selected' }))
    await user.click(screen.getByRole('button', { name: 'Update' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Add at least one component line.')
    expect(apiPut).not.toHaveBeenCalled()
  })

  it('lets a component line be corrected in place instead of deleted and re-added', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const lineQuantity = await screen.findByDisplayValue('24')
    await user.clear(lineQuantity)
    await user.type(lineQuantity, '30')
    await user.click(screen.getByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    const body = JSON.parse(JSON.stringify(apiPut.mock.calls[0][1])) as Record<string, unknown>
    const lines = body.ProductionOrderLines as Array<Record<string, unknown>>
    expect(lines).toHaveLength(1)
    expect(lines[0].PlannedQuantity).toBe(30)
    expect(lines[0].ItemNo).toBe('RM-100')
    expect(lines[0].UoMCode).toBe(6)
  })

  it('surfaces an approval deferral instead of pretending the order reached SAP', async () => {
    const user = userEvent.setup()
    apiPut.mockResolvedValue({ pendingApproval: true, pendingApprovalRequestId: 31 })
    render(
      <MemoryRouter initialEntries={['/production-orders/form/646']}>
        <Routes>
          <Route path="/production-orders/form/:id" element={<ProductionOrderFormPage />} />
          <Route path="/my-approval-requests" element={<div>Approval requests queue</div>} />
        </Routes>
      </MemoryRouter>,
    )

    await user.click(await screen.findByRole('button', { name: 'Update' }))

    expect(await screen.findByText('Approval requests queue')).toBeInTheDocument()
  })

  it('shows an error instead of a blank form when the order does not exist', async () => {
    apiGet.mockRejectedValue(new Error('The requested resource was not found.'))
    renderEditForm()

    expect(await screen.findByRole('alert')).toHaveTextContent('The requested resource was not found.')
    expect(screen.queryByRole('button', { name: 'Update' })).not.toBeInTheDocument()
  })
})
