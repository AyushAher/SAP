import { useEffect, useState } from 'react'
import {
  formatCodeWithName,
  resolveBusinessPartner,
  resolveItem,
  resolveProject,
} from '@/helpers/masterLookup'
import type { ProductionOrder } from '@/types/production'

interface ProductionOrderDetailsPanelProps {
  order?: ProductionOrder | null
  projectName?: string
}

function DetailItem({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-medium text-slate-900">{value ?? '—'}</div>
    </div>
  )
}

export function ProductionOrderDetailsPanel({ order, projectName }: ProductionOrderDetailsPanelProps) {
  const [resolvedCustomerName, setResolvedCustomerName] = useState<string>()
  const [resolvedProjectName, setResolvedProjectName] = useState<string>()
  const [resolvedItemName, setResolvedItemName] = useState<string>()

  useEffect(() => {
    if (!order) {
      setResolvedCustomerName(undefined)
      setResolvedProjectName(undefined)
      setResolvedItemName(undefined)
      return
    }

    let cancelled = false
    void (async () => {
      const [customer, project, item] = await Promise.all([
        order.CustomerCode && !order.CustomerName
          ? resolveBusinessPartner(order.CustomerCode)
          : Promise.resolve(undefined),
        order.Project && !projectName && !order.ProjectName
          ? resolveProject(order.Project)
          : Promise.resolve(undefined),
        order.ItemNumber && !order.ProductDescription
          ? resolveItem(order.ItemNumber)
          : Promise.resolve(undefined),
      ])

      if (cancelled) return
      setResolvedCustomerName(customer?.CardName)
      setResolvedProjectName(project?.Name)
      setResolvedItemName(item?.ItemName)
    })()

    return () => {
      cancelled = true
    }
  }, [order, projectName])

  if (!order) {
    return (
      <div className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-sm text-slate-500">
        Select a production order to view details.
      </div>
    )
  }

  const customerDisplay = formatCodeWithName(order.CustomerCode, order.CustomerName ?? resolvedCustomerName)
  const projectDisplay = formatCodeWithName(order.Project, projectName ?? order.ProjectName ?? resolvedProjectName)
  const itemDisplay = formatCodeWithName(order.ItemNumber, order.ProductDescription ?? resolvedItemName)

  return (
    <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-4">
      <h3 className="text-base font-semibold text-slate-900">Production Order Details</h3>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <DetailItem label="Production Order" value={order.DocumentNumber} />
        <DetailItem label="Customer" value={customerDisplay} />
        <DetailItem label="Project" value={projectDisplay} />
        <DetailItem label="Receipt Warehouse" value={order.Warehouse} />
        <DetailItem label="Item" value={itemDisplay} />
        <DetailItem label="Drawing Number" value={order.DrawingNo} />
      </div>
    </div>
  )
}
