import { Link } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Card, CardContent, CardHeader, CardTitle } from '@/Components/ui'
import { ROUTES } from '@/config/constants'

const modules = [
  { title: 'Purchase Orders', to: ROUTES.PURCHASE_ORDERS, desc: 'Create and manage POs with stage-wise payments' },
  { title: 'Stock Transfers', to: ROUTES.INVENTORY_TRANSFERS, desc: 'Inventory transfer requests' },
  { title: 'Production Orders', to: ROUTES.PRODUCTION_ORDERS, desc: 'SAP production order management' },
  { title: 'Issue For Production', to: ROUTES.ISSUE_FOR_PRODUCTION, desc: 'Issue material for production' },
  { title: 'Receipt From Production', to: ROUTES.RECEIPT_FROM_PRODUCTION, desc: 'Receipt material from production' },
  { title: 'Approvals', to: ROUTES.APPROVALS, desc: 'Review pending approval requests' },
  { title: 'My Requests', to: ROUTES.MY_APPROVAL_REQUESTS, desc: 'Track your submitted requests' },
  { title: 'Business Partner', to: ROUTES.BUSINESS_PARTNER, desc: 'Sync vendor/customer to SAP' },
  { title: 'GRPO', to: ROUTES.GRPO, desc: 'Goods receipt from purchase order' },
]

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Home" description="Welcome to ConnectEdge — your enterprise integration hub" />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {modules.map((m) => (
          <Link key={m.to} to={m.to}>
            <Card className="h-full transition-shadow hover:shadow-md">
              <CardHeader><CardTitle>{m.title}</CardTitle></CardHeader>
              <CardContent><p className="text-sm text-slate-500">{m.desc}</p></CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  )
}
