import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as apiClient from '@/helpers/api/client'
import { ProductionOrderFormPage } from './ProductionOrderFormPage'
import { clearCreateDraft, saveCreateDraft } from '@/helpers/productionOrderCreateDraft'

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
  nameFromCodeWithNameLabel: (label?: string | null, code?: string | null) => {
    const text = (label ?? '').trim()
    const prefix = `${code ?? ''} - `
    return text.startsWith(prefix) ? text.slice(prefix.length).trim() || undefined : undefined
  },
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

const savedSubassembly = {
  AbsoluteEntry: -1,
  ParentProductionOrderNo: '10/1',
  DrawingNo: 'PBBPL-A-1234-1',
  ProductDescription: 'Steam piping spool',
  Weight: 12.5,
  ProductionOrderLines: [
    { ItemNo: 'CHANNEL-200', ItemName: 'Channel', PlannedQuantity: 24, IssuedQuantity: 2, Warehouse: 'Store1' },
  ],
}

function renderCreateForm() {
  return render(
    <MemoryRouter initialEntries={['/production-orders/form']}>
      <Routes>
        <Route path="/production-orders/form/:id?" element={<ProductionOrderFormPage />} />
        <Route path="/production-orders/form/:id/subassemblies/:childId?" element={<div>Sub-assembly form</div>} />
        <Route path="/production-orders" element={<div>Production order list</div>} />
      </Routes>
    </MemoryRouter>,
  )
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
    clearCreateDraft()
    apiPost.mockResolvedValue({})
    apiPut.mockResolvedValue({ AbsoluteEntry: 646, DocumentNumber: 10 })
    apiGet.mockImplementation(async (url: string) => {
      if (url === '/production-orders/646') return sapOrder as never
      if (url.startsWith('/production-orders/646/subassemblies')) return [savedSubassembly] as never
      if (url === '/masters/sales-orders/156') {
        return {
          DocEntry: 156,
          DocNum: 252610128,
          DocumentLines: [
            { ItemCode: 'FG-001', ItemDescription: 'Finished pump', Quantity: 12, UnitsOfMeasurment: 1, InventoryQuantity: 12 },
          ],
        } as never
      }
      return {} as never
    })
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
    expect(body.U_PrjName).toBe('Refinery upgrade')
    expect(body.ProductionOrderType).toBe('bopotSpecial')
    expect(body.ProductionOrderOriginNumber).toBe(252610128)
    expect(body.ProductionOrderOriginEntry).toBe(156)
    expect(body.DueDate).toBe('2026-06-27')
    expect(body).not.toHaveProperty('Status')
    expect(body).not.toHaveProperty('ProductionOrderLines')
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
    expect(body).not.toHaveProperty('ProductionOrderLines')
  })

  it('warns that sub-assembly item quantities need reviewing when the header quantity changes', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const quantity = await screen.findByLabelText(/^Planned Qty/)
    await user.clear(quantity)
    await user.type(quantity, '8')

    expect(await screen.findByRole('status')).toHaveTextContent(/review sub-assembly item quantities/i)
    expect(screen.queryByDisplayValue('24')).not.toBeInTheDocument()
  })

  it('updates an existing order that has no sub-assemblies', async () => {
    apiGet.mockImplementation(async (url: string) => {
      if (url === '/production-orders/646') return sapOrder as never
      if (url.startsWith('/production-orders/646/subassemblies')) return [] as never
      if (url === '/masters/sales-orders/156') {
        return {
          DocEntry: 156,
          DocNum: 252610128,
          DocumentLines: [
            { ItemCode: 'FG-001', ItemDescription: 'Finished pump', Quantity: 12, UnitsOfMeasurment: 1, InventoryQuantity: 12 },
          ],
        } as never
      }
      return {} as never
    })
    const user = userEvent.setup()
    renderEditForm()

    await user.click(await screen.findByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('updates an existing SAP order that has no sales order', async () => {
    apiGet.mockImplementation(async (url: string) => {
      if (url === '/production-orders/646') {
        return {
          ...sapOrder,
          ProductionOrderOriginNumber: undefined,
          ProductionOrderOriginEntry: undefined,
        } as never
      }
      if (url.startsWith('/production-orders/646/subassemblies')) return [] as never
      return {} as never
    })
    const user = userEvent.setup()
    renderEditForm()

    await screen.findByRole('button', { name: 'Update' })
    expect(screen.getByText('Sales Order').parentElement?.textContent).not.toMatch(/\*/)
    await user.click(screen.getByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('keeps a Standard production order type instead of rewriting it to Special', async () => {
    apiGet.mockImplementation(async (url: string) => {
      if (url === '/production-orders/646') {
        return { ...sapOrder, ProductionOrderType: 'bopotStandard' } as never
      }
      if (url.startsWith('/production-orders/646/subassemblies')) return [] as never
      if (url === '/masters/sales-orders/156') {
        return {
          DocEntry: 156,
          DocNum: 252610128,
          DocumentLines: [
            { ItemCode: 'FG-001', ItemDescription: 'Finished pump', Quantity: 12, UnitsOfMeasurment: 1, InventoryQuantity: 12 },
          ],
        } as never
      }
      return {} as never
    })
    const user = userEvent.setup()
    renderEditForm()

    await user.click(await screen.findByRole('button', { name: 'Update' }))

    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    const body = JSON.parse(JSON.stringify(apiPut.mock.calls[0][1])) as Record<string, unknown>
    expect(body.ProductionOrderType).toBe('bopotStandard')
  })

  it('still requires a sub-assembly with items when creating', async () => {
    saveCreateDraft({
      header: {
        ItemNumber: 'FG-001',
        ProductDescription: 'Finished pump',
        Warehouse: 'Subcon',
        IssWarehouse: 'Store1',
        PlannedQuantity: 12,
        SalesOrderDocNum: 252610128,
        SalesOrderDocEntry: 156,
        CustomerCode: 'C000017',
        ProductionCategory: 'JOB',
        Status: 'boposPlanned',
      },
      subassemblies: [],
    })
    const user = userEvent.setup()
    renderCreateForm()

    await user.click(await screen.findByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Add at least one sub-assembly with items.')
    expect(apiPost).not.toHaveBeenCalled()
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

  it('shows sub-assemblies once the parent production order is saved in SAP', async () => {
    renderEditForm()

    expect(await screen.findByRole('tab', { name: 'Sub-assemblies' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Add Sub-assembly' })).toHaveAttribute(
      'href',
      '/production-orders/form/646/subassemblies',
    )
    expect(await screen.findByText('Issued Qty')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '2' })).toBeInTheDocument()
  })

  it('lets the user add a sub-assembly while creating a new production order', async () => {
    const user = userEvent.setup()
    renderCreateForm()

    expect(await screen.findByRole('button', { name: 'Add Sub-assembly' })).toBeInTheDocument()
    expect(screen.getByText(/created in SAP when you save this production order/i)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Add Sub-assembly' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Add Sub-assembly' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Product No. is required.')
    expect(apiPost).not.toHaveBeenCalled()
  })

  it('saves the parent and drafted sub-assemblies in one create request', async () => {
    saveCreateDraft({
      header: {
        ItemNumber: 'FG-001',
        ProductDescription: 'Finished pump',
        Warehouse: 'Subcon',
        IssWarehouse: 'Store1',
        PlannedQuantity: 12,
        SalesOrderDocNum: 252610128,
        SalesOrderDocEntry: 156,
        CustomerCode: 'C000017',
        ProductionCategory: 'JOB',
        Status: 'boposPlanned',
      },
      subassemblies: [{
        DraftKey: 'draft-1',
        ParentProductionOrderNo: '0/1',
        DrawingNo: 'DWG-1',
        ProductDescription: 'Spool',
        ProductionOrderLines: [{ ItemNo: 'CHANNEL-200', PlannedQuantity: 2, Warehouse: 'Store1' }],
      }],
    })
    apiPost.mockResolvedValue({ AbsoluteEntry: 671, DocumentNumber: 35 })
    const user = userEvent.setup()
    renderCreateForm()

    expect(await screen.findByLabelText('Product Name')).toHaveValue('Finished pump')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    await waitFor(() => expect(apiPost).toHaveBeenCalledTimes(1))
    expect(apiPost.mock.calls[0][0]).toBe('/production-orders')
    const body = apiPost.mock.calls[0][1] as Record<string, unknown>
    expect(body.Subassemblies).toEqual([
      expect.objectContaining({
        U_DwgNo: 'DWG-1',
        U_DocNum: '0/1',
        ProductionOrderLines: [
          expect.objectContaining({ ItemNo: 'CHANNEL-200', PlannedQuantity: 2 }),
        ],
      }),
    ])
    expect(body).not.toHaveProperty('ProductionOrderLines')
  })

  it('lays out the header in four columns and keeps remarks in the footer', async () => {
    renderEditForm()

    expect(await screen.findByLabelText('Production Order')).toHaveValue('10')
    expect(screen.getByLabelText('Product Name')).toHaveValue('Finished pump')
    expect(screen.queryByRole('combobox', { name: /^Type$/i })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Drawing No.')).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/Receipt Warehouse/i)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/Issuing Warehouse/i)).not.toBeInTheDocument()
    expect(screen.queryByText('Production Order Lines')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Remarks')).toBeInTheDocument()

    const header = screen.getByLabelText('Production Order')
    const subassemblies = screen.getByRole('tab', { name: 'Sub-assemblies' })
    const remarks = screen.getByLabelText('Remarks')
    expect(header.compareDocumentPosition(subassemblies) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(subassemblies.compareDocumentPosition(remarks) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('caps planned quantity at the sales order quantity × items per unit', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const quantity = await screen.findByLabelText(/^Planned Qty/)
    await user.clear(quantity)
    await user.type(quantity, '20')

    expect(quantity).toHaveValue(12)
    await user.click(screen.getByRole('button', { name: 'Update' }))
    await waitFor(() => expect(apiPut).toHaveBeenCalledTimes(1))
    const body = JSON.parse(JSON.stringify(apiPut.mock.calls[0][1])) as Record<string, unknown>
    expect(body.PlannedQuantity).toBe(12)
  })
})
