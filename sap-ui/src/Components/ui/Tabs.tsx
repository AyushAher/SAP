import { createContext, useContext, useId, type ReactNode } from 'react'
import { cn } from '@/helpers/lib/utils'

interface TabsContextValue {
  value: string
  onChange: (value: string) => void
  baseId: string
}

const TabsContext = createContext<TabsContextValue | null>(null)

function useTabsContext() {
  const context = useContext(TabsContext)
  if (!context) throw new Error('Tabs components must be used within Tabs')
  return context
}

export interface TabsProps {
  value: string
  onValueChange: (value: string) => void
  children: ReactNode
  className?: string
}

export function Tabs({ value, onValueChange, children, className }: TabsProps) {
  const baseId = useId()
  return (
    <TabsContext.Provider value={{ value, onChange: onValueChange, baseId }}>
      <div className={cn('space-y-0', className)}>{children}</div>
    </TabsContext.Provider>
  )
}

export interface TabsListProps {
  children: ReactNode
  className?: string
  'aria-label'?: string
}

export function TabsList({ children, className, 'aria-label': ariaLabel }: TabsListProps) {
  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className={cn(
        'scrollbar-thin -mb-px flex gap-0 overflow-x-auto border-b border-slate-200 bg-gradient-to-b from-slate-50/80 to-transparent px-1',
        className,
      )}
    >
      {children}
    </div>
  )
}

export interface TabsTriggerProps {
  value: string
  children: ReactNode
  icon?: ReactNode
  badge?: string | number
  className?: string
  disabled?: boolean
}

export function TabsTrigger({ value, children, icon, badge, className, disabled }: TabsTriggerProps) {
  const { value: activeValue, onChange, baseId } = useTabsContext()
  const selected = activeValue === value
  const tabId = `${baseId}-tab-${value}`
  const panelId = `${baseId}-panel-${value}`

  return (
    <button
      type="button"
      role="tab"
      id={tabId}
      aria-selected={selected}
      aria-controls={panelId}
      disabled={disabled}
      onClick={() => onChange(value)}
      className={cn(
        'group relative flex shrink-0 items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 focus-visible:ring-offset-2',
        selected
          ? 'border-primary-600 text-primary-700'
          : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-800',
        disabled && 'cursor-not-allowed opacity-50',
        className,
      )}
    >
      {icon && (
        <span
          className={cn(
            'flex h-5 w-5 items-center justify-center transition-colors',
            selected ? 'text-primary-600' : 'text-slate-400 group-hover:text-slate-600',
          )}
          aria-hidden="true"
        >
          {icon}
        </span>
      )}
      <span>{children}</span>
      {badge != null && badge !== '' && Number(badge) !== 0 && (
        <span
          className={cn(
            'inline-flex min-w-[1.25rem] items-center justify-center rounded-full px-1.5 py-0.5 text-xs font-semibold tabular-nums',
            selected ? 'bg-primary-100 text-primary-700' : 'bg-slate-100 text-slate-600',
          )}
        >
          {badge}
        </span>
      )}
    </button>
  )
}

export interface TabsContentProps {
  value: string
  children: ReactNode
  className?: string
  title?: string
  description?: string
}

export function TabsContent({ value, children, className, title, description }: TabsContentProps) {
  const { value: activeValue, baseId } = useTabsContext()
  if (activeValue !== value) return null

  const tabId = `${baseId}-tab-${value}`
  const panelId = `${baseId}-panel-${value}`

  return (
    <div
      role="tabpanel"
      id={panelId}
      aria-labelledby={tabId}
      className={cn('pt-6', className)}
    >
      {(title || description) && (
        <div className="mb-5 border-b border-slate-100 pb-4">
          {title && <h3 className="text-base font-semibold text-slate-900">{title}</h3>}
          {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
        </div>
      )}
      {children}
    </div>
  )
}
