import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as productionOrders from '@/Requests/productionOrders'
import { SubassemblyFormPage } from './SubassemblyFormPage'

vi.mock('@/Requests/productionOrders', () => ({
  getProductionOrder: vi.fn(),
  listSubassemblies: vi.fn(),
  createProductionOrder: vi.fn(),
  updateProductionOrder: vi.fn(),
}))

const getProductionOrder = vi.mocked(productionOrders.getProductionOrder)
const listSubassemblies = vi.mocked(productionOrders.listSubassemblies)
const createProductionOrder = vi.mocked(productionOrders.createProductionOrder)

const parentOrder = {
  AbsoluteEntry: 646,
  DocumentNumber: 10,
  ItemNumber: 'FG-001',
  ProductDescription: 'FINISHED GOOD',
  Status: 'boposPlanned',
  Warehouse: 'WIP',
  PlannedQuantity: 12,
  SalesOrderDocNum: 252610128,
  SalesOrderDocEntry: 156,
  CustomerCode: 'C000017',
  Project: 'PRJ-1',
}

function renderNewSubassembly() {
  return render(
    <MemoryRouter initialEntries={['/production-orders/form/646/subassemblies']}>
      <Routes>
        <Route path="/production-orders/form/:id/subassemblies/:childId?" element={<SubassemblyFormPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SubassemblyFormPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    createProductionOrder.mockResolvedValue({ AbsoluteEntry: 700, DocumentNumber: 21 })
    getProductionOrder.mockResolvedValue(parentOrder)
    listSubassemblies.mockResolvedValue([])
  })

  it('inherits the parent product and numbers the child as parent/sequence', async () => {
    const user = userEvent.setup()
    renderNewSubassembly()

    expect(await screen.findByDisplayValue('10/1')).toBeInTheDocument()
    expect(screen.getByDisplayValue('FG-001 - FINISHED GOOD')).toBeInTheDocument()
    expect(screen.queryByPlaceholderText('Search item...')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(createProductionOrder).toHaveBeenCalledTimes(1)
    const body = createProductionOrder.mock.calls[0][0]
    expect(body.ItemNumber).toBe('FG-001')
    expect(body.ParentProductionOrderNo).toBe('10/1')
    expect(body.ProductDescription).toBe('')
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
})
