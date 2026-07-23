import { NavLink } from 'react-router-dom'
import {
  Home,
  ArrowLeftRight,
  ShoppingCart,
  Factory,
  PackageMinus,
  PackagePlus,
  CheckSquare,
  ClipboardList,
  Shield,
  Users,
  UsersRound,
  ChevronLeft,
  ChevronRight,
  LogOut,
} from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import { ROUTES, ROLES } from '@/config/constants'
import { ConnectEdgeLogo } from '@/Components/brand/ConnectEdgeLogo'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleSidebar, setMobileSidebarOpen } from '@/store/slices/uiSlice'
import { logoutUser } from '@/store/slices/authSlice'

const navItems = [
  { to: ROUTES.HOME, label: 'Home', icon: Home },
  { to: ROUTES.INVENTORY_TRANSFERS, label: 'Stock Transfer', icon: ArrowLeftRight },
  { to: ROUTES.PURCHASE_ORDERS, label: 'Purchase Order', icon: ShoppingCart },
  { to: ROUTES.ISSUE_FOR_PRODUCTION, label: 'Issue For Production', icon: PackageMinus },
  { to: ROUTES.RECEIPT_FROM_PRODUCTION, label: 'Receipt From Production', icon: PackagePlus },
  { to: ROUTES.APPROVALS, label: 'Approvals', icon: CheckSquare },
  { to: ROUTES.MY_APPROVAL_REQUESTS, label: 'My Approval Requests', icon: ClipboardList },
  { to: ROUTES.PRODUCTION_ORDERS, label: 'Production Order', icon: Factory },
]

const adminItems = [
  { to: ROUTES.APPROVAL_POLICIES, label: 'Approval Policies', icon: Shield },
  { to: ROUTES.USER_GROUPS, label: 'User Groups', icon: UsersRound },
  { to: ROUTES.USER_ROLES, label: 'User Role Management', icon: Users },
]

function isAdmin(roles?: string[]) {
  if (!roles?.length) return false
  return roles.some((r) => r === ROLES.SUPER_ADMIN || r === ROLES.ADMIN)
}

export function Sidebar() {
  const dispatch = useAppDispatch()
  const collapsed = useAppSelector((state) => state.ui.sidebarCollapsed)
  const mobileOpen = useAppSelector((state) => state.ui.sidebarMobileOpen)
  const user = useAppSelector((state) => state.auth.user)

  const handleNavClick = () => {
    dispatch(setMobileSidebarOpen(false))
  }

  const handleLogout = () => {
    dispatch(logoutUser())
    handleNavClick()
  }

  const showAdmin = isAdmin(user?.roles ?? (user?.role ? [user.role] : []))

  const renderLink = ({ to, label, icon: Icon }: { to: string; label: string; icon: React.ComponentType<{ className?: string }> }) => (
    <NavLink
      key={to}
      to={to}
      onClick={handleNavClick}
      className={({ isActive }) =>
        cn(
          'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
          isActive
            ? 'border-l-2 border-cyan-400 bg-sidebar-active text-white'
            : 'border-l-2 border-transparent text-slate-300 hover:bg-sidebar-hover hover:text-white',
          collapsed && 'justify-center px-2',
        )
      }
      title={collapsed ? label : undefined}
    >
      <Icon className="h-5 w-5 shrink-0" />
      {!collapsed && <span>{label}</span>}
    </NavLink>
  )

  return (
    <>
      {mobileOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 lg:hidden"
          onClick={() => dispatch(setMobileSidebarOpen(false))}
        />
      )}

      <aside
        className={cn(
          'fixed left-0 top-0 z-50 flex h-full flex-col bg-sidebar text-white transition-all duration-300',
          collapsed ? 'w-16' : 'w-64',
          mobileOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0',
        )}
      >
        <div
          className={cn(
            'flex h-16 items-center border-b border-white/10 px-4',
            collapsed ? 'flex-col justify-center gap-1 py-2' : 'justify-between',
          )}
        >
          {collapsed ? (
            <ConnectEdgeLogo variant="icon" className="h-7 w-7" />
          ) : (
            <ConnectEdgeLogo />
          )}
          <button
            type="button"
            onClick={() => dispatch(toggleSidebar())}
            className="hidden rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-sidebar-hover hover:text-white lg:block"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {collapsed ? <ChevronRight className="h-5 w-5" /> : <ChevronLeft className="h-5 w-5" />}
          </button>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3 scrollbar-thin">
          {navItems.map(renderLink)}
          {showAdmin && (
            <>
              {!collapsed && <p className="px-3 pt-4 text-xs font-semibold uppercase tracking-wider text-slate-500">Admin</p>}
              {adminItems.map(renderLink)}
            </>
          )}
        </nav>

        <div className="border-t border-white/10 p-3">
          {!collapsed && user && (
            <div className="mb-3 px-3">
              <p className="truncate text-sm font-medium text-white">{user.name}</p>
              <p className="truncate text-xs text-slate-400">{user.email}</p>
            </div>
          )}
          <button
            type="button"
            onClick={handleLogout}
            className={cn(
              'flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium',
              'text-slate-300 transition-colors hover:bg-sidebar-hover hover:text-white',
              collapsed && 'justify-center px-2',
            )}
            title={collapsed ? 'Logout' : undefined}
          >
            <LogOut className="h-5 w-5 shrink-0" />
            {!collapsed && <span>Logout</span>}
          </button>
        </div>
      </aside>
    </>
  )
}
