import {
  applyPaginationRequest,
  createDefaultPaginationRequest,
  createPaginationResponse,
} from '@/helpers/api/pagination'
import type { PaginationRequest, PaginationResponse } from '@/types/api'
import type { User } from '@/types'

const MOCK_USERS: User[] = [
  { id: '1', name: 'John Doe', email: 'john@company.com', role: 'admin' },
  { id: '2', name: 'Jane Smith', email: 'jane@company.com', role: 'editor' },
  { id: '3', name: 'Bob Wilson', email: 'bob@company.com', role: 'viewer' },
  { id: '4', name: 'Alice Johnson', email: 'alice@company.com', role: 'admin' },
  { id: '5', name: 'Charlie Brown', email: 'charlie@company.com', role: 'editor' },
  { id: '6', name: 'Diana Prince', email: 'diana@company.com', role: 'admin' },
  { id: '7', name: 'Edward Norton', email: 'edward@company.com', role: 'viewer' },
  { id: '8', name: 'Fiona Green', email: 'fiona@company.com', role: 'editor' },
  { id: '9', name: 'George Martin', email: 'george@company.com', role: 'viewer' },
  { id: '10', name: 'Hannah Lee', email: 'hannah@company.com', role: 'admin' },
  { id: '11', name: 'Ian Wright', email: 'ian@company.com', role: 'editor' },
  { id: '12', name: 'Julia Roberts', email: 'julia@company.com', role: 'viewer' },
  { id: '13', name: 'Kevin Hart', email: 'kevin@company.com', role: 'admin' },
  { id: '14', name: 'Laura Palmer', email: 'laura@company.com', role: 'editor' },
  { id: '15', name: 'Michael Scott', email: 'michael@company.com', role: 'viewer' },
  { id: '16', name: 'Nina Simone', email: 'nina@company.com', role: 'admin' },
  { id: '17', name: 'Oscar Wilde', email: 'oscar@company.com', role: 'editor' },
  { id: '18', name: 'Paula Abdul', email: 'paula@company.com', role: 'viewer' },
  { id: '19', name: 'Quincy Jones', email: 'quincy@company.com', role: 'admin' },
  { id: '20', name: 'Rachel Green', email: 'rachel@company.com', role: 'editor' },
  { id: '21', name: 'Steve Rogers', email: 'steve@company.com', role: 'viewer' },
  { id: '22', name: 'Tina Turner', email: 'tina@company.com', role: 'admin' },
  { id: '23', name: 'Uma Thurman', email: 'uma@company.com', role: 'editor' },
  { id: '24', name: 'Victor Hugo', email: 'victor@company.com', role: 'viewer' },
]

const ACCESSORS = {
  name: (row: User) => row.name,
  email: (row: User) => row.email,
  role: (row: User) => row.role,
}

/**
 * Fetch users with optional pagination, filters, and sorts.
 * Callable from DataTable or directly from any page:
 *
 *   const users = await GetUsers();
 *   const usersList = users.data ?? [];
 */
export async function GetUsers(
  request?: PaginationRequest,
): Promise<PaginationResponse<User[]>> {
  const req = request ?? createDefaultPaginationRequest()

  await new Promise((resolve) => setTimeout(resolve, 400))

  const { items, totalCount } = applyPaginationRequest(MOCK_USERS, req, ACCESSORS)

  return createPaginationResponse(items, req, { totalCount })
}
