import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AuthLayout } from '@/layouts/AuthLayout'
import { MainLayout } from '@/layouts/MainLayout'
import { AuthGuard } from '@/routes/guards/AuthGuard'
import { GuestGuard } from '@/routes/guards/GuestGuard'
import { LoginPage } from '@/Pages/auth/LoginPage'
import { RegisterPage } from '@/Pages/auth/RegisterPage'
import { ForgotPasswordPage } from '@/Pages/auth/ForgotPasswordPage'
import { DashboardPage } from '@/Pages/dashboard/DashboardPage'
import { PurchaseOrderListPage } from '@/Pages/purchase-orders/PurchaseOrderListPage'
import { PurchaseOrderFormPage } from '@/Pages/purchase-orders/PurchaseOrderFormPage'
import { StageWisePaymentPage } from '@/Pages/purchase-orders/StageWisePaymentPage'
import { StageWisePaymentBatchPage } from '@/Pages/purchase-orders/StageWisePaymentBatchPage'
import { InventoryTransferListPage } from '@/Pages/inventory-transfers/InventoryTransferListPage'
import { StockTransferFormPage } from '@/Pages/inventory-transfers/StockTransferFormPage'
import { ProductionOrderListPage } from '@/Pages/production-orders/ProductionOrderListPage'
import { ProductionOrderFormPage } from '@/Pages/production-orders/ProductionOrderFormPage'
import { IssueForProductionListPage } from '@/Pages/production/IssueForProductionListPage'
import { IssueForProductionFormPage } from '@/Pages/production/IssueForProductionFormPage'
import { ReceiptFromProductionListPage } from '@/Pages/production/ReceiptFromProductionListPage'
import { ReceiptFromProductionFormPage } from '@/Pages/production/ReceiptFromProductionFormPage'
import { ApprovalsPage } from '@/Pages/approvals/ApprovalsPage'
import { MyApprovalRequestsPage } from '@/Pages/approvals/MyApprovalRequestsPage'
import { ApprovalPoliciesPage } from '@/Pages/approvals/ApprovalPoliciesPage'
import { UserGroupsPage } from '@/Pages/users/UserGroupsPage'
import { UserRoleManagementPage } from '@/Pages/users/UserRoleManagementPage'
import { BusinessPartnerPage } from '@/Pages/business-partner/BusinessPartnerPage'
import { GrpoPage } from '@/Pages/grpo/GrpoPage'
import { ROUTES } from '@/config/constants'

export const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <AuthGuard>
        <MainLayout />
      </AuthGuard>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: ROUTES.PURCHASE_ORDERS.slice(1), element: <PurchaseOrderListPage /> },
      { path: 'purchase-orders/form/:id?', element: <PurchaseOrderFormPage /> },
      { path: 'purchase-orders/:id/payments', element: <StageWisePaymentPage /> },
      { path: 'purchase-orders/:id/payments/batch', element: <StageWisePaymentBatchPage /> },
      { path: 'purchase-orders/:id/payments/batch/approve/:approvalRequestId', element: <StageWisePaymentBatchPage /> },
      { path: 'purchase-orders/:id/payments/batch/payment/:stageWisePaymentId', element: <StageWisePaymentBatchPage /> },
      { path: 'purchase-orders/:id/payments/batch/:batchId', element: <StageWisePaymentBatchPage /> },
      { path: ROUTES.INVENTORY_TRANSFERS.slice(1), element: <InventoryTransferListPage /> },
      { path: 'inventory-transfers/form/:id?', element: <StockTransferFormPage /> },
      { path: ROUTES.PRODUCTION_ORDERS.slice(1), element: <ProductionOrderListPage /> },
      { path: 'production-orders/form/:id?', element: <ProductionOrderFormPage /> },
      { path: ROUTES.ISSUE_FOR_PRODUCTION.slice(1), element: <IssueForProductionListPage /> },
      { path: 'issue-for-production/form/:id?', element: <IssueForProductionFormPage /> },
      { path: ROUTES.RECEIPT_FROM_PRODUCTION.slice(1), element: <ReceiptFromProductionListPage /> },
      { path: 'receipt-from-production/form/:id?', element: <ReceiptFromProductionFormPage /> },
      { path: ROUTES.APPROVALS.slice(1), element: <ApprovalsPage /> },
      { path: ROUTES.MY_APPROVAL_REQUESTS.slice(1), element: <MyApprovalRequestsPage /> },
      { path: ROUTES.APPROVAL_POLICIES.slice(1), element: <ApprovalPoliciesPage /> },
      { path: ROUTES.USER_GROUPS.slice(1), element: <UserGroupsPage /> },
      { path: ROUTES.USER_ROLES.slice(1), element: <UserRoleManagementPage /> },
      { path: ROUTES.BUSINESS_PARTNER.slice(1), element: <BusinessPartnerPage /> },
      { path: ROUTES.GRPO.slice(1), element: <GrpoPage /> },
    ],
  },
  {
    path: '/auth',
    element: (
      <GuestGuard>
        <AuthLayout />
      </GuestGuard>
    ),
    children: [
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
    ],
  },
  {
    path: '*',
    element: <Navigate to={ROUTES.HOME} replace />,
  },
])
