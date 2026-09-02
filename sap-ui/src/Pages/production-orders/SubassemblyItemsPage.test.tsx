import React from 'react'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { SubassemblyItemsPage } from './SubassemblyItemsPage'

describe('SubassemblyItemsPage', () => {
  it('redirects item editing onto the combined sub-assembly screen', () => {
    render(
      <MemoryRouter initialEntries={['/production-orders/form/646/subassemblies/700/items']}>
        <Routes>
          <Route
            path="/production-orders/form/:id/subassemblies/:childId/items"
            element={<SubassemblyItemsPage />}
          />
          <Route
            path="/production-orders/form/:id/subassemblies/:childId"
            element={<div>Combined sub-assembly</div>}
          />
        </Routes>
      </MemoryRouter>,
    )

    expect(screen.getByText('Combined sub-assembly')).toBeInTheDocument()
  })
})
