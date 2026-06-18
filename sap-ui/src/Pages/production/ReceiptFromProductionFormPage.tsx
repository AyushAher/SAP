import { ProductionRequestForm } from '@/Components/production/ProductionRequestForm'
import { ROUTES } from '@/config/constants'
import { getReceiptFromProductionOrderLines, saveReceiptFromProduction } from '@/Requests/receiptFromProduction'

export function ReceiptFromProductionFormPage() {
  return (
    <ProductionRequestForm
      title="Receipt From Production Request"
      listRoute={ROUTES.RECEIPT_FROM_PRODUCTION}
      loadOrderLines={getReceiptFromProductionOrderLines}
      saveOrderLines={saveReceiptFromProduction}
    />
  )
}
