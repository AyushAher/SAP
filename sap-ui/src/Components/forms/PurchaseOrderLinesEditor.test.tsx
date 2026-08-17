import React from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PurchaseOrderLinesEditor } from './PurchaseOrderLinesEditor'
import { PO_DOC_TYPE } from '@/helpers/purchaseOrderTnValidation'
import { searchWarehouses } from '@/Requests/masters'
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
  searchWarehouses: vi.fn().mockResolvedValue({
    data: [{ WarehouseCode: 'Store5', WarehouseName: 'Store 5', Location: 2 }],
  }),
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

  it('lets the user pick a G/L account when the item is non-inventory', async () => {
    const user = userEvent.setup()
    render(<PurchaseOrderLinesEditor lines={[{
      ...itemLine,
      InventoryItem: 'tNO',
      AccountCode: undefined,
      AccountLabel: undefined,
    }]} onChange={vi.fn()} />)
    await user.click(screen.getByTitle('Edit item'))
    const account = await screen.findByLabelText('G/L Account *')
    expect(account).not.toBeDisabled()
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

describe('PurchaseOrderLinesEditor — service lines', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows Loc. on the service grid', () => {
    render(
      <PurchaseOrderLinesEditor
        docType={PO_DOC_TYPE.service}
        defaultWarehouse="Store5"
        lines={[{
          ItemDescription: 'Freight',
          AccountCode: '600000',
          LocationCode: 2,
          LocationLabel: '2',
        }]}
        onChange={vi.fn()}
      />,
    )
    expect(screen.getByText('Loc.')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByText('Freight')).toBeInTheDocument()
  })

  it('reads service description from camelCase API fields', () => {
    render(
      <PurchaseOrderLinesEditor
        docType={PO_DOC_TYPE.service}
        lines={[{
          itemDescription: 'Engineering support',
          AccountCode: '600000',
        } as PurchaseOrderLineItem]}
        onChange={vi.fn()}
      />,
    )
    expect(screen.getByText('Engineering support')).toBeInTheDocument()
  })

  it('fills Loc. from the header warehouse when adding a service line', async () => {
    const user = userEvent.setup()
    render(
      <PurchaseOrderLinesEditor
        docType={PO_DOC_TYPE.service}
        defaultWarehouse="Store5"
        lines={[]}
        onChange={vi.fn()}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Add Service' }))
    await waitFor(() => expect(screen.getByLabelText('Loc.')).toHaveValue('2'))
    expect(searchWarehouses).toHaveBeenCalled()
  })
})
