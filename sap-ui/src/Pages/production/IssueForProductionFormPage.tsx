import { ProductionRequestForm } from '@/Components/production/ProductionRequestForm'
import { ROUTES } from '@/config/constants'
import { getIssueForProductionOrderLines, saveIssueForProduction } from '@/Requests/issueForProduction'

export function IssueForProductionFormPage() {
  return (
    <ProductionRequestForm
      title="Issue For Production Request"
      listRoute={ROUTES.ISSUE_FOR_PRODUCTION}
      loadOrderLines={getIssueForProductionOrderLines}
      saveOrderLines={saveIssueForProduction}
    />
  )
}
