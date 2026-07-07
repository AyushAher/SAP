import { Outlet } from 'react-router-dom'
import { cn } from '@/helpers/lib/utils'
import { useAppSelector } from '@/store/hooks'
import { Sidebar } from './components/Sidebar'
import { Header } from './components/Header'
import { AuthSessionListener } from './components/AuthSessionListener'

export function MainLayout() {
  const collapsed = useAppSelector((state) => state.ui.sidebarCollapsed)

  return (
    <div className="min-h-screen bg-slate-50">
      <AuthSessionListener />
      <Sidebar />
      <div
        className={cn(
          'flex min-h-screen min-w-0 flex-col transition-all duration-300',
          collapsed ? 'lg:pl-16' : 'lg:pl-64',
        )}
      >
        <Header />
        <main className="min-w-0 flex-1 p-4 lg:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
