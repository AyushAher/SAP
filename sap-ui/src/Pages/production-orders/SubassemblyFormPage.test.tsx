import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import * as apiClient from '@/helpers/api/client'
import { SubassemblyFormPage } from './SubassemblyFormPage'

vi.mock('@/helpers/api/client', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  invalidateCachedGets: vi.fn(),
}))

vi.mock('@/helpers/masterLookup', () => ({
  formatCodeWithName: (code?: string | number | null, name?: string | null) =>
    [code, name].filter(Boolean).join(' - ') || '—',
  resolveSelectOptionByCode: vi.fn().mockResolvedValue(undefined),
  resolveItem: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/Requests/masters', () => ({
  searchItems: vi.fn().mockResolvedValue({ data: [] }),
}))

const apiGet = vi.mocked(apiClient.apiGet)
const apiPost = vi.mocked(apiClient.apiPost)

const parentOrder = {
  AbsoluteEntry: 646,
  DocumentNumber: 10,
  ItemNo: 'FG-001',
  ProductionOrderStatus: 'boposPlanned',
  Warehouse: 'WIP',
  PlannedQuantity: 12,
  ProductionOrderOriginNumber: 252610128,
  ProductionOrderOriginEntry: 156,
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
    apiPost.mockResolvedValue({ AbsoluteEntry: 700, DocumentNumber: 21 })
    apiGet.mockImplementation(async (url: string) => (
      url === '/production-orders/646' ? parentOrder : {}
    ) as never)
  })

  it('requires a product before creating the child production order', async () => {
    const user = userEvent.setup()
    renderNewSubassembly()

    await user.click(await screen.findByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Product No. is required.')
    expect(apiPost).not.toHaveBeenCalled()
  })
})
