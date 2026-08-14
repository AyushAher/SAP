import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PurchaseOrderLinesEditor } from './PurchaseOrderLinesEditor'
import type { PurchaseOrderLineItem } from '@/types/purchaseOrder'

vi.mock('@/Requests/masters', () => ({
  listPurchaseUoms: vi.fn().mockResolvedValue([]),
  lookupItem: vi.fn().mockResolvedValue(undefined),
  searchGlAccounts: vi.fn().mockResolvedValue({ data: [] }),
  searchHsnCodes: vi.fn().mockResolvedValue({ data: [] }),
  searchItems: vi.fn().mockResolvedValue({ data: [] }),
  searchProjects: vi.fn().mockResolvedValue({ data: [] }),
  searchSacCodes: vi.fn().mockResolvedValue({ data: [] }),
  searchTaxCodes: vi.fn().mockResolvedValue({ data: [] }),
  searchWarehouses: vi.fn().mockResolvedValue({ data: [] }),
  formatWarehouseOptionLabel: (wh: { WarehouseCode?: string }) => wh.WarehouseCode ?? '',
}))

vi.mock('@/hooks/useItemMasterMap', () => ({ useItemMasterMap: () => ({}) }))

const itemLine: PurchaseOrderLineItem = {
  ItemCode: 'RM5703813500380',
  ItemDescription: 'BEAM 250 MM',
  Quantity: 1600,
  StockQty: 120,
  UnitsOfMeasurment: 0.075,
  // As SAP returns it for an item on the Manual UoM group.
  UoMCode: 'Manual',
  MeasureUnit: 'KGS',
  StockUom: 'MTR',
  UnitPrice: 62,
  TaxCode: 'IGST18',
  HSNEntry: 19,
  HsnLabel: '72.16.32 - ANGLES, BEAMS, CHANNELS, FLAT',
  AccountCode: '_SYS00000000893',
  AccountLabel: '_SYS00000000893 - Raw material',
}

describe('PurchaseOrderLinesEditor — item lines', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows the readable purchase unit instead of the Manual placeholder', () => {
    render(<PurchaseOrderLinesEditor lines={[itemLine]} onChange={vi.fn()} />)
    expect(screen.getByText('KGS')).toBeInTheDocument()
    expect(screen.queryByText('Manual')).not.toBeInTheDocument()
  })

  it('shows HSN with its description', () => {
    render(<PurchaseOrderLinesEditor lines={[itemLine]} onChange={vi.fn()} />)
    expect(screen.getByText('72.16.32 - ANGLES, BEAMS, CHANNELS, FLAT')).toBeInTheDocument()
  })

  it('shows the G/L account SAP determined for the row as read-only', async () => {
    const user = userEvent.setup()
    render(<PurchaseOrderLinesEditor lines={[itemLine]} onChange={vi.fn()} />)
    expect(screen.getByText('G/L Account')).toBeInTheDocument()
    expect(screen.getByText('_SYS00000000893 - Raw material')).toBeInTheDocument()
    await user.click(screen.getByTitle('Edit item'))
    const account = await screen.findByLabelText('G/L Account')
    expect(account).toBeDisabled()
  })

  it('keeps a fractional factor intact while it is being typed', async () => {
    const user = userEvent.setup()
    render(<PurchaseOrderLinesEditor lines={[itemLine]} onChange={vi.fn()} />)
    await user.click(screen.getByTitle('Edit item'))

    const itemsPerUnit = await screen.findByLabelText('Items per Unit')
    await user.clear(itemsPerUnit)
    await user.type(itemsPerUnit, '0.027')
    expect(itemsPerUnit).toHaveValue(0.027)
    expect(screen.getByLabelText(/Stock Qty/)).toHaveValue(43.2)
  })

  it('keeps a typed items-per-unit and recalculates the stock quantity', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<PurchaseOrderLinesEditor lines={[itemLine]} onChange={onChange} />)
    await user.click(screen.getByTitle('Edit item'))

    const itemsPerUnit = await screen.findByLabelText('Items per Unit')
    await user.clear(itemsPerUnit)
    await user.type(itemsPerUnit, '0.05')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    await waitFor(() => expect(onChange).toHaveBeenCalled())
    const [saved] = onChange.mock.calls[0][0] as PurchaseOrderLineItem[]
    expect(saved.UnitsOfMeasurment).toBeCloseTo(0.05, 10)
    expect(saved.StockQty).toBeCloseTo(80, 10)
    expect(saved.MeasureUnit).toBe('KGS')
    expect(saved.AccountCode).toBe('_SYS00000000893')
  })
})
