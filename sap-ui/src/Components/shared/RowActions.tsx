import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { Link, type LinkProps } from 'react-router-dom'
import { cn } from '@/helpers/lib/utils'

type RowActionVariant = 'default' | 'danger' | 'primary'

const baseClassName =
  'inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border transition-colors focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-1 disabled:cursor-not-allowed disabled:opacity-50'

const variantClassName: Record<RowActionVariant, string> = {
  default: 'border-slate-300 bg-white text-slate-600 hover:bg-slate-50',
  primary: 'border-primary-200 bg-primary-50 text-primary-700 hover:bg-primary-100',
  danger: 'border-red-200 bg-white text-red-600 hover:bg-red-50',
}

export interface RowActionButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  title: string
  icon: ReactNode
  variant?: RowActionVariant
}

export const RowActionButton = forwardRef<HTMLButtonElement, RowActionButtonProps>(
  ({ title, icon, variant = 'default', className, type = 'button', ...props }, ref) => (
    <button
      ref={ref}
      type={type}
      title={title}
      aria-label={title}
      className={cn(baseClassName, variantClassName[variant], className)}
      {...props}
    >
      {icon}
    </button>
  ),
)

RowActionButton.displayName = 'RowActionButton'

export interface RowActionLinkProps extends LinkProps {
  title: string
  icon: ReactNode
  variant?: RowActionVariant
}

export function RowActionLink({
  title,
  icon,
  variant = 'default',
  className,
  ...props
}: RowActionLinkProps) {
  return (
    <Link
      title={title}
      aria-label={title}
      className={cn(baseClassName, variantClassName[variant], className)}
      {...props}
    >
      {icon}
    </Link>
  )
}

export function RowActions({
  children,
  className,
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('inline-flex flex-nowrap items-center gap-1', className)}>
      {children}
    </div>
  )
}

export const rowActionsCellClassName = 'w-px whitespace-nowrap align-middle'
export const rowActionsHeaderClassName = 'w-px whitespace-nowrap'

export const rowActionIconClassName = 'h-4 w-4'
