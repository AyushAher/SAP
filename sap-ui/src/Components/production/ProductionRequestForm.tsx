import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Trash2 } from 'lucide-react'
import { useNavigate, useParams } from 'react-router-dom'
import { ProductionOrderDetailsPanel } from '@/Components/production/ProductionOrderDetailsPanel'
import { ProductionOrderLinesDialog } from '@/Components/production/ProductionOrderLinesDialog'
import { ProductionOrderSelectionDialog } from '@/Components/production/ProductionOrderSelectionDialog'
import { PageHeader } from '@/Components/shared/PageHeader'
import { PreviousNextButtons } from '@/Components/shared/PreviousNextButtons'
import { RowActionButton, RowActions, rowActionIconClassName } from '@/Components/shared/RowActions'
import { SapDataGrid, type SapColumn } from '@/Components/shared/SapDataGrid'
import { Button, Card, CardContent, Input, SearchableSelect } from '@/Components/ui'
import { searchItems, searchWarehouses, formatWarehouseOptionLabel, CONSUMABLES_ITEM_GROUP_FILTER } from '@/Requests/masters'
import { formatCodeWithName } from '@/helpers/masterLookup'
import { useItemMasterMap } from '@/hooks/useItemMasterMap'
import { addProductionOrderLine, selectProductionOrder } from '@/Requests/productionOrders'
import {
  validateManualProductionLineQuantities,
  validateProductionRequestForSave,
} from '@/helpers/productionRequestValidation'
import {
  alreadyIssuedQuantity,
} from '@/helpers/productionRequestLines'
import type { SelectOption } from '@/types'
import type { ProductionOrder, ProductionOrderLine, ProductionOrderSelection } from '@/types/production'

interface ProductionRequestFormProps {
  title: string
  listRoute: string
  loadOrderLines: (id: number) => Promise<ProductionOrderSelection | null>
  saveOrderLines: (orderLines: ProductionOrderSelection, id?: number) => Promise<{ id?: number }>
  downloadPdf?: (id: number) => Promise<void>
  showWorkerName?: boolean
  consumableItemsOnly?: boolean
  variant?: 'issue' | 'receipt'
}

const emptyLine = (): ProductionOrderLine => ({
  ItemNo: '',
  PlannedQuantity: 0,
  IssuedQuantity: 0,
  Warehouse: '',
})

export function ProductionRequestForm({
  title,
  listRoute,
  loadOrderLines,
  saveOrderLines,
  downloadPdf,
  showWorkerName = false,
  consumableItemsOnly = false,
  variant = 'issue',
}: ProductionRequestFormProps) {
  const { id } = useParams()
  const navigate = useNavigate()
  const [selection, setSelection] = useState<ProductionOrderSelection | null>(null)
  const [projectName, setProjectName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [selectionOpen, setSelectionOpen] = useState(false)
  const [linesDialogOpen, setLinesDialogOpen] = useState(false)
  const [pendingOrder, setPendingOrder] = useState<ProductionOrderSelection | null>(null)
  const [manualLine, setManualLine] = useState<ProductionOrderLine>(emptyLine())
  const [itemLabel, setItemLabel] = useState('')
  const [warehouseLabel, setWarehouseLabel] = useState('')
  const [addingLine, setAddingLine] = useState(false)
  const [loading, setLoading] = useState(!!id)
  const [saving, setSaving] = useState(false)
  const [workerName, setWorkerName] = useState('')
  const isReceipt = variant === 'receipt'

  const lineItemCodes = useMemo(
    () => [
      ...(selection?.ProductionOrderLinesEntryNumber ?? []).map((line) => line.ItemNo),
      manualLine.ItemNo,
    ],
    [selection?.ProductionOrderLinesEntryNumber, manualLine.ItemNo],
  )
  const itemMasterMap = useItemMasterMap(lineItemCodes)

  useEffect(() => {
    if (!id) return
    loadOrderLines(Number(id))
      .then((data) => {
        if (data) {
          setSelection(data)
          setProjectName(data.ProductionOrder?.ProjectName ?? '')
          setWorkerName(data.WorkerName ?? '')
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load request'))
      .finally(() => setLoading(false))
  }, [id, loadOrderLines, isReceipt])

  const searchItemOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchItems(
      search,
      20,
      undefined,
      consumableItemsOnly ? [CONSUMABLES_ITEM_GROUP_FILTER] : [],
    )
    return (response.data ?? []).map((item) => ({
      value: item.ItemCode ?? '',
      label: `${item.ItemCode ?? ''} - ${item.ItemName ?? ''}`.trim(),
    })).filter((o) => o.value)
  }, [consumableItemsOnly])

  const searchWarehouseOptions = useCallback(async (search: string): Promise<SelectOption[]> => {
    const response = await searchWarehouses(search)
    return (response.data ?? []).map((wh) => ({
      value: wh.WarehouseCode ?? '',
      label: formatWarehouseOptionLabel(wh),
    })).filter((o) => o.value)
  }, [])

  const lineColumns: SapColumn<ProductionOrderLine>[] = useMemo(() => {
    const columns: SapColumn<ProductionOrderLine>[] = [
      {
        key: 'sr',
        header: 'Sr. No.',
        accessor: (r) => {
          const lines = selection?.ProductionOrderLinesEntryNumber ?? []
          const index = lines.findIndex(
            (line) => line.LineNumber === r.LineNumber && line.ItemNo === r.ItemNo,
          )
          return index >= 0 ? index + 1 : ''
        },
      },
      { key: 'LineNumber', header: 'Line No.', accessor: (r) => r.LineNumber },
      {
        key: 'ItemNo',
        header: 'Item',
        accessor: (r) => formatCodeWithName(r.ItemNo, r.ItemName ?? itemMasterMap[r.ItemNo ?? '']?.name),
      },
      { key: 'PlannedQuantity', header: 'Planned Qty.', accessor: (r) => r.PlannedQuantity },
    ]

    if (isReceipt) {
      columns.push({
        key: 'AlreadyIssued',
        header: 'Issued Qty',
        accessor: (r) => alreadyIssuedQuantity(selection?.ProductionOrder, r),
      })
    }

    columns.push(
      {
        key: 'IssuedQuantity',
        header: isReceipt ? 'Receipt Qty' : 'Issue Qty.',
        render: (row) => (
          <Input
            type="number"
            value={String(row.IssuedQuantity ?? 0)}
            onChange={(e) => {
              const value = Number(e.target.value)
              setSelection((prev) => {
                if (!prev) return prev
                const lines = prev.ProductionOrderLinesEntryNumber.map((line) =>
                  line.LineNumber === row.LineNumber && line.ItemNo === row.ItemNo
                    ? { ...line, IssuedQuantity: value }
                    : line,
                )
                return { ...prev, ProductionOrderLinesEntryNumber: lines }
              })
            }}
          />
        ),
      },
      { key: 'uom', header: 'Stock UOM', accessor: (r) => (r.ItemNo ? itemMasterMap[r.ItemNo]?.uom ?? '' : '') },
      { key: 'Warehouse', header: isReceipt ? 'Receipt Warehouse' : 'Issuing Warehouse', accessor: (r) => r.Warehouse },
    )

    return columns
  }, [isReceipt, itemMasterMap, selection?.ProductionOrder, selection?.ProductionOrderLinesEntryNumber])

  const handleSelection = async (picked: ProductionOrder) => {
    if (!picked.AbsoluteEntry) return
    const orderSelection = await selectProductionOrder(String(picked.AbsoluteEntry))
    if (!orderSelection.ProductionOrder?.AbsoluteEntry) {
      throw new Error('Production order details could not be loaded.')
    }
    setPendingOrder(orderSelection)
    setLinesDialogOpen(true)
  }

  const handleLinesConfirm = (lines: ProductionOrderLine[]) => {
    if (!pendingOrder) return
    setSelection({
      ProductionOrder: pendingOrder.ProductionOrder,
      ProductionOrderLinesEntryNumber: lines,
    })
    setProjectName(pendingOrder.ProductionOrder.ProjectName ?? '')
    setPendingOrder(null)
    setLinesDialogOpen(false)
  }

  const handleDeleteLine = (row: ProductionOrderLine) => {
    setSelection((prev) => {
      if (!prev) return prev
      return {
        ...prev,
        ProductionOrderLinesEntryNumber: prev.ProductionOrderLinesEntryNumber.filter(
          (line) => !(line.LineNumber === row.LineNumber && line.ItemNo === row.ItemNo),
        ),
      }
    })
  }

  const handleAddManualLine = async (e: FormEvent) => {
    e.preventDefault()
    if (!selection?.ProductionOrder?.AbsoluteEntry) {
      setError('Please select a production order first.')
      return
    }
    if (!manualLine.ItemNo) {
      setError('Item is required.')
      return
    }
    if (!manualLine.Warehouse) {
      setError('Warehouse is required.')
      return
    }
    const qtyError = validateManualProductionLineQuantities(
      manualLine.IssuedQuantity,
      manualLine.PlannedQuantity,
    )
    if (qtyError) {
      setError(qtyError)
      return
    }
    setAddingLine(true)
    setError(null)
    try {
      const result = await addProductionOrderLine(String(selection.ProductionOrder.AbsoluteEntry), manualLine)
      setSelection((prev) => {
        if (!prev) return prev
        const updatedOrder = result.ProductionOrder
          ? { ...result.ProductionOrder }
          : { ...prev.ProductionOrder }
        const existingPoLines = updatedOrder.ProductionOrderLines ?? prev.ProductionOrder.ProductionOrderLines ?? []
        const alreadyOnPo = existingPoLines.some(
          (line) => line.LineNumber === result.AddedLine.LineNumber && line.ItemNo === result.AddedLine.ItemNo,
        )
        updatedOrder.ProductionOrderLines = alreadyOnPo
          ? existingPoLines
          : [...existingPoLines, result.AddedLine]

        return {
          ...prev,
          ProductionOrder: updatedOrder,
          ProductionOrderLinesEntryNumber: [
            ...prev.ProductionOrderLinesEntryNumber,
            result.AddedLine,
          ],
        }
      })
      setManualLine(emptyLine())
      setItemLabel('')
      setWarehouseLabel('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add line')
    } finally {
      setAddingLine(false)
    }
  }

  const handleSubmit = async () => {
    const validationError = validateProductionRequestForSave(selection)
    if (validationError) {
      setError(validationError)
      return
    }
    setSaving(true)
    setError(null)
    try {
      await saveOrderLines(
        { ...selection!, WorkerName: workerName },
        id ? Number(id) : undefined,
      )
      navigate(listRoute)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="py-12 text-center text-slate-500">Loading...</div>

  const hasProductionOrder = !!selection?.ProductionOrder?.AbsoluteEntry
  const showWorkerField = showWorkerName && hasProductionOrder

  return (
    <div className="space-y-6">
      <PageHeader
        title={title}
        action={downloadPdf && id ? (
          <Button variant="outline" onClick={() => downloadPdf(Number(id))}>Download PDF</Button>
        ) : undefined}
      />

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <ProductionOrderDetailsPanel
        order={selection?.ProductionOrder}
        projectName={projectName}
        showWorkerName={showWorkerField}
        workerName={workerName}
        onWorkerNameChange={setWorkerName}
      />

      <Card>
        <CardContent className="space-y-4 pt-6">
          <SapDataGrid
            columns={lineColumns}
            data={selection?.ProductionOrderLinesEntryNumber ?? []}
            getRowKey={(row) => `${row.LineNumber ?? 'fg'}-${row.ItemNo}`}
            emptyMessage="No lines selected"
            actions={(row) => (
              <RowActions>
                <RowActionButton
                  title="Delete item"
                  variant="danger"
                  icon={<Trash2 className={rowActionIconClassName} />}
                  onClick={() => handleDeleteLine(row)}
                />
              </RowActions>
            )}
          />
          <Button type="button" variant="outline" onClick={() => setSelectionOpen(true)}>
            Select Production Order
          </Button>
        </CardContent>
      </Card>

      {!isReceipt && (
      <Card>
        <CardContent className="space-y-4 pt-6">
          <h3 className="text-sm font-semibold text-slate-800">Add To Production Order</h3>
          <form onSubmit={handleAddManualLine} className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
            <SearchableSelect
              label="Item"
              lookupKind="item"
              value={manualLine.ItemNo ?? ''}
              selectedLabel={itemLabel}
              placeholder="Search item..."
              onSearch={searchItemOptions}
              onChange={(code, option) => {
                setItemLabel(option?.label ?? code)
                setManualLine({ ...manualLine, ItemNo: code })
              }}
            />
            <Input label="Item No." value={manualLine.ItemNo ?? ''} readOnly />
            <Input
              label="Planned Qty."
              type="number"
              value={String(manualLine.PlannedQuantity ?? 0)}
              onChange={(e) => setManualLine({ ...manualLine, PlannedQuantity: Number(e.target.value) })}
              required
            />
            <Input
              label="Issued Qty."
              type="number"
              value={String(manualLine.IssuedQuantity ?? 0)}
              onChange={(e) => setManualLine({ ...manualLine, IssuedQuantity: Number(e.target.value) })}
              required
            />
            <Input
              label="Stock UOM"
              value={manualLine.ItemNo ? itemMasterMap[manualLine.ItemNo]?.uom ?? '' : ''}
              readOnly
            />
            <SearchableSelect
              label="Warehouse"
              value={manualLine.Warehouse ?? ''}
              selectedLabel={warehouseLabel}
              placeholder="Search warehouse..."
              onSearch={searchWarehouseOptions}
              onChange={(code, option) => {
                setWarehouseLabel(option?.label ?? code)
                setManualLine({ ...manualLine, Warehouse: code })
              }}
            />
            <div className="md:col-span-3 xl:col-span-6">
              <Button type="submit" variant="outline" isLoading={addingLine}>Add To Production Order</Button>
            </div>
          </form>
        </CardContent>
      </Card>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button onClick={handleSubmit} isLoading={saving}>{id ? 'Update Request' : 'Create Request'}</Button>
        <Button variant="outline" onClick={() => navigate(listRoute)}>Cancel</Button>
        <PreviousNextButtons
          id={id}
          onPrevious={id && Number(id) > 1 ? () => navigate(`${listRoute}/form/${Number(id) - 1}`) : undefined}
          onNext={id ? () => navigate(`${listRoute}/form/${Number(id) + 1}`) : undefined}
        />
      </div>

      <ProductionOrderSelectionDialog
        isOpen={selectionOpen}
        onClose={() => setSelectionOpen(false)}
        onSelected={handleSelection}
      />

      <ProductionOrderLinesDialog
        isOpen={linesDialogOpen}
        order={pendingOrder?.ProductionOrder}
        includeFinishedGood={isReceipt}
        warehouseHeader={isReceipt ? 'Receipt Warehouse' : 'Issuing Warehouse'}
        onClose={() => {
          setLinesDialogOpen(false)
          setPendingOrder(null)
        }}
        onConfirm={handleLinesConfirm}
      />
    </div>
  )
}
