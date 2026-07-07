export const APP_NAME = 'ConnectEdge'
export const APP_TAGLINE = 'Enterprise Integration Platform'

export const STORAGE_KEYS = {
  TOKEN: 'sap_token',
  REFRESH_TOKEN: 'sap_refresh_token',
  USER: 'sap_user',
  COMPANY_DB: 'sap_company_db',
  BRANCH_ID: 'sap_branch_id',
} as const

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

export const ROUTES = {
  LOGIN: '/auth/login',
  REGISTER: '/auth/register',
  FORGOT_PASSWORD: '/auth/forgot-password',
  HOME: '/',
  DASHBOARD: '/',
  PURCHASE_ORDERS: '/purchase-orders',
  PURCHASE_ORDER_FORM: '/purchase-orders/form',
  PURCHASE_ORDER_EDIT: '/purchase-orders/form/:id',
  STAGE_WISE_PAYMENT: '/purchase-orders/:id/payments',
  INVENTORY_TRANSFERS: '/inventory-transfers',
  INVENTORY_TRANSFER_FORM: '/inventory-transfers/form',
  INVENTORY_TRANSFER_EDIT: '/inventory-transfers/form/:id',
  PRODUCTION_ORDERS: '/production-orders',
  PRODUCTION_ORDER_FORM: '/production-orders/form',
  PRODUCTION_ORDER_EDIT: '/production-orders/form/:id',
  ISSUE_FOR_PRODUCTION: '/issue-for-production',
  ISSUE_FOR_PRODUCTION_FORM: '/issue-for-production/form',
  ISSUE_FOR_PRODUCTION_EDIT: '/issue-for-production/form/:id',
  RECEIPT_FROM_PRODUCTION: '/receipt-from-production',
  RECEIPT_FROM_PRODUCTION_FORM: '/receipt-from-production/form',
  RECEIPT_FROM_PRODUCTION_EDIT: '/receipt-from-production/form/:id',
  APPROVALS: '/approvals',
  MY_APPROVAL_REQUESTS: '/my-approval-requests',
  APPROVAL_POLICIES: '/approval-policies',
  USER_ROLES: '/user-roles',
  BUSINESS_PARTNER: '/business-partner',
  GRPO: '/grpo',
  USERS: '/users',
  SETTINGS: '/settings',
} as const

export const ROLES = {
  SUPER_ADMIN: 'SuperAdmin',
  ADMIN: 'Admin',
  STANDARD: 'Standard',
} as const

export const API_ERROR_CODES = {
  INCORRECT_CREDENTIALS: 'AUTH-01',
  SAP_SESSION_UNAVAILABLE: 'AUTH-02',
} as const
