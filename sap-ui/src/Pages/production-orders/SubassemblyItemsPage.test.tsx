import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as productionOrders from '@/Requests/productionOrders'
import { SubassemblyItemsPage } from './SubassemblyItemsPage'

vi.mock('@/Requests/productionOrders', () => ({
  getProductionOrder: vi.fn(),
  updateProductionOrder: vi.fn(),
}))

vi.mock('@/helpers/masterLookup', () => ({
  formatCodeWithName: (code?: string | number | null, name?: string | null) =>
    [code, name].filter(Boolean).join(' - ') || '—',
  resolveSelectOptionByCode: vi.fn().mockResolvedValue(undefined),
  resolveItem: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/hooks/useItemMasterMap', () => ({ useItemMasterMap: () => ({}) }))

vi.mock('@/Requests/masters', () => ({
  searchItems: vi.fn().mockResolvedValue({ data: [] }),
}))

const getProductionOrder = vi.mocked(productionOrders.getProductionOrder)
const updateProductionOrder = vi.mocked(productionOrders.updateProductionOrder)

describe('SubassemblyItemsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    updateProductionOrder.mockResolvedValue({ AbsoluteEntry: 700, DocumentNumber: 21 })
    getProductionOrder.mockImplementation(async (id) => {
      if (String(id) === '646') {
        return { AbsoluteEntry: 646, DocumentNumber: 10, ItemNumber: 'FG-001' }
      }
      return {
        AbsoluteEntry: 700,
        DocumentNumber: 21,
        ItemNumber: 'FG-001',
        DrawingNo: 'DWG-SA',
        ProductDescription: 'Firewall drawing',
        ParentProductionOrderNo: '10/1',
        Warehouse: 'WIP',
        PlannedQuantity: 2,
        ProductionOrderLines: [],
      }
    })
  })

  it('requires at least one item with a code and quantity before saving', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/production-orders/form/646/subassemblies/700/items']}>
        <Routes>
          <Route
            path="/production-orders/form/:id/subassemblies/:childId/items"
            element={<SubassemblyItemsPage />}
          />
        </Routes>
      </MemoryRouter>,
    )

    await screen.findByText('10/1')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Add at least one item.')
    expect(updateProductionOrder).not.toHaveBeenCalled()
  })
})
