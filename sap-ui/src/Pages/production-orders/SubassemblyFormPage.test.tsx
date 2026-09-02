import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as productionOrders from '@/Requests/productionOrders'
import * as masters from '@/Requests/masters'
import { SubassemblyFormPage } from './SubassemblyFormPage'
import { clearCreateDraft, loadCreateDraft, saveCreateDraft } from '@/helpers/productionOrderCreateDraft'

vi.mock('@/Requests/productionOrders', () => ({
  getProductionOrder: vi.fn(),
  listSubassemblies: vi.fn(),
  createProductionOrder: vi.fn(),
  updateProductionOrder: vi.fn(),
}))

vi.mock('@/Requests/masters', () => ({
  searchItems: vi.fn(),
}))

vi.mock('@/helpers/masterLookup', () => ({
  resolveSelectOptionByCode: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/hooks/useItemMasterMap', () => ({ useItemMasterMap: () => ({}) }))

const getProductionOrder = vi.mocked(productionOrders.getProductionOrder)
const listSubassemblies = vi.mocked(productionOrders.listSubassemblies)
const createProductionOrder = vi.mocked(productionOrders.createProductionOrder)
const updateProductionOrder = vi.mocked(productionOrders.updateProductionOrder)
const searchItems = vi.mocked(masters.searchItems)

const parentOrder = {
  AbsoluteEntry: 646,
  DocumentNumber: 10,
  ItemNumber: 'FG-001',
  ProductDescription: 'FINISHED GOOD',
  Status: 'boposPlanned',
  Warehouse: 'WIP',
  IssWarehouse: 'Store1',
  PlannedQuantity: 12,
  SalesOrderDocNum: 252610128,
  SalesOrderDocEntry: 156,
  CustomerCode: 'C000017',
  Project: 'PRJ-1',
  ProjectName: 'Refinery upgrade',
  ProductionOrderLines: [{ LineNumber: 0, ItemNo: 'RM-100', ItemName: 'Steel', PlannedQuantity: 4, Warehouse: 'WIP' }],
}

function renderNewSubassembly() {
  return render(
    <MemoryRouter initialEntries={['/production-orders/form/646/subassemblies']}>
      <Routes>
        <Route path="/production-orders/form/:id/subassemblies/:childId?" element={<SubassemblyFormPage />} />
        <Route path="/production-orders/form/:id" element={<div>Parent production order</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SubassemblyFormPage', () => {
  beforeEach(() => {
    Element.prototype.scrollIntoView = vi.fn()
    vi.clearAllMocks()
    clearCreateDraft()
    createProductionOrder.mockResolvedValue({ AbsoluteEntry: -1, DocumentNumber: 10 })
    updateProductionOrder.mockResolvedValue({ AbsoluteEntry: -1, DocumentNumber: 10 })
    getProductionOrder.mockResolvedValue(parentOrder)
    listSubassemblies.mockResolvedValue([])
    searchItems.mockResolvedValue({
      data: [{ ItemCode: 'CHANNEL-200', ItemName: 'Channel' }],
    } as never)
  })

  it('inherits the parent numbering and requires items before creating', async () => {
    const user = userEvent.setup()
    renderNewSubassembly()

    expect(await screen.findByDisplayValue('10/1')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: /Item Code/i })).toBeInTheDocument()
    expect(screen.getByLabelText('Drawing No.')).toBeInTheDocument()
    expect(screen.getByLabelText('Drawing Name')).toBeInTheDocument()
    expect(screen.getByLabelText('Weight')).toBeInTheDocument()
    expect(screen.getByText('Issued Qty')).toBeInTheDocument()
    expect(screen.getByLabelText('Free Text')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Add$/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Add at least one item.')
    expect(createProductionOrder).not.toHaveBeenCalled()
  })

  it('increments the sequence from existing siblings including cancelled', async () => {
    listSubassemblies.mockResolvedValue([
      { AbsoluteEntry: 700, ParentProductionOrderNo: '10/1' },
      { AbsoluteEntry: 701, ParentProductionOrderNo: '10/2' },
    ])

    renderNewSubassembly()

    expect(await screen.findByDisplayValue('10/3')).toBeInTheDocument()
    expect(listSubassemblies).toHaveBeenCalledWith('646', { includeCancelled: true })
  })

  it('saves a drafted sub-assembly locally without calling the API', async () => {
    const user = userEvent.setup()
    saveCreateDraft({
      header: {
        ItemNumber: 'FG-001',
        ProductDescription: 'FINISHED GOOD',
        Warehouse: 'WIP',
        IssWarehouse: 'Store1',
        PlannedQuantity: 12,
        SalesOrderDocNum: 252610128,
      },
      subassemblies: [],
    })

    render(
      <MemoryRouter initialEntries={['/production-orders/form/new/subassemblies']}>
        <Routes>
          <Route path="/production-orders/form/:id/subassemblies/:childId?" element={<SubassemblyFormPage />} />
          <Route path="/production-orders/form" element={<div>Parent production order</div>} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByDisplayValue('Pending/1')).toBeInTheDocument()
    await user.click(screen.getByRole('combobox', { name: /Item Code/i }))
    await user.click(await screen.findByRole('option', { name: /CHANNEL-200/ }))
    await user.clear(screen.getByLabelText('Qty'))
    await user.type(screen.getByLabelText('Qty'), '2')
    await user.click(screen.getByRole('button', { name: 'Add item' }))
    await user.click(screen.getByRole('button', { name: /^Add$/ }))

    expect(createProductionOrder).not.toHaveBeenCalled()
    expect(updateProductionOrder).not.toHaveBeenCalled()
    expect(await screen.findByText('Parent production order')).toBeInTheDocument()
    const stored = loadCreateDraft()?.subassemblies ?? []
    expect(stored).toHaveLength(1)
    expect(stored[0].ParentProductionOrderNo).toBe('0/1')
    expect(stored[0].ProductionOrderLines).toEqual([
      expect.objectContaining({ ItemNo: 'CHANNEL-200', PlannedQuantity: 2 }),
    ])
  })

  it('saves Drawing Name onto the child product description on update', async () => {
    const user = userEvent.setup()
    updateProductionOrder.mockResolvedValue({ AbsoluteEntry: 661, DocumentNumber: 25 })
    getProductionOrder.mockImplementation(async (id) => {
      if (String(id) === '646') return parentOrder
      return {
        ...parentOrder,
        AbsoluteEntry: 661,
        DocumentNumber: 25,
        ParentProductionOrderNo: '0/1',
        DrawingNo: 'PBBPL-A-1234-1',
        ProductDescription: 'FINISHED GOOD',
        Weight: 8,
        ProductionOrderLines: [
          { ItemNo: 'CHANNEL-200', ItemName: 'Channel', PlannedQuantity: 24, IssuedQuantity: 2, Warehouse: 'Store1' },
        ],
      }
    })

    render(
      <MemoryRouter initialEntries={['/production-orders/form/646/subassemblies/661']}>
        <Routes>
          <Route path="/production-orders/form/:id/subassemblies/:childId?" element={<SubassemblyFormPage />} />
          <Route path="/production-orders/form/:id" element={<div>Parent production order</div>} />
        </Routes>
      </MemoryRouter>,
    )

    expect(await screen.findByText('CHANNEL-200')).toBeInTheDocument()
    expect(screen.getByText('Issued Qty')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '2' })).toBeInTheDocument()
    expect(screen.getByTitle('Issued items cannot be deleted')).toBeDisabled()
    const drawingName = screen.getByLabelText('Drawing Name')
    await user.clear(drawingName)
    await user.type(drawingName, 'Steam piping spool')
    await user.click(screen.getByRole('button', { name: 'Update' }))

    expect(updateProductionOrder).toHaveBeenCalledTimes(1)
    expect(updateProductionOrder.mock.calls[0][0]).toBe(661)
    expect(updateProductionOrder.mock.calls[0][1].ProductDescription).toBe('Steam piping spool')
    expect(updateProductionOrder.mock.calls[0][1].DrawingNo).toBe('PBBPL-A-1234-1')
    expect(updateProductionOrder.mock.calls[0][1].ParentProductionOrderNo).toBe('10/1')
    expect(updateProductionOrder.mock.calls[0][1].Weight).toBe(8)
    expect(updateProductionOrder.mock.calls[0][1].ProductionOrderLines).toEqual([
      expect.objectContaining({ ItemNo: 'CHANNEL-200', PlannedQuantity: 24 }),
    ])
  })
})
