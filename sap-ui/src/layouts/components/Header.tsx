import { Menu, Bell, Search } from 'lucide-react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleMobileSidebar } from '@/store/slices/uiSlice'
import { Input } from '@/Components/ui'
import { CompanySwitcher } from '@/layouts/components/CompanySwitcher'
import { BranchSwitcher } from '@/layouts/components/BranchSwitcher'

export function Header() {
  const dispatch = useAppDispatch()
  const user = useAppSelector((state) => state.auth.user)

  return (
    <header
      className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-slate-200 bg-white px-4 lg:px-6"
      style={{ marginLeft: 0 }}
    >
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={() => dispatch(toggleMobileSidebar())}
          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 lg:hidden"
          aria-label="Open menu"
        >
          <Menu className="h-5 w-5" />
        </button>
        <div className="hidden w-72 md:block">
          <Input
            placeholder="Search..."
            leftIcon={<Search className="h-4 w-4" />}
            className="!py-1.5"
          />
        </div>
      </div>

      <div className="flex items-center gap-3">
        <CompanySwitcher />
        <BranchSwitcher />
        <button
          type="button"
          className="relative rounded-lg p-2 text-slate-500 hover:bg-slate-100"
          aria-label="Notifications"
        >
          <Bell className="h-5 w-5" />
          <span className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full bg-red-500" />
        </button>
        {user && (
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-600 text-sm font-medium text-white">
              {user.name.charAt(0).toUpperCase()}
            </div>
            <span className="hidden text-sm font-medium text-slate-700 sm:block">
              {user.name}
            </span>
          </div>
        )}
      </div>
    </header>
  )
}
