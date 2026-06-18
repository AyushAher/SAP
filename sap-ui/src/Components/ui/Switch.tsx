import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/helpers/lib/utils'

export interface SwitchProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string
  description?: string
}

export const Switch = forwardRef<HTMLInputElement, SwitchProps>(
  ({ className, label, description, id, ...props }, ref) => {
    const inputId = id ?? label?.toLowerCase().replace(/\s+/g, '-')

    return (
      <label htmlFor={inputId} className="flex cursor-pointer items-center justify-between gap-4">
        {(label || description) && (
          <div className="flex-1">
            {label && <span className="text-sm font-medium text-slate-700">{label}</span>}
            {description && <p className="text-sm text-slate-500">{description}</p>}
          </div>
        )}
        <div className="relative">
          <input ref={ref} type="checkbox" id={inputId} className="peer sr-only" role="switch" {...props} />
          <div
            className={cn(
              'h-6 w-11 rounded-full bg-slate-200 transition-colors',
              'peer-checked:bg-primary-600 peer-focus:ring-2 peer-focus:ring-primary-500 peer-focus:ring-offset-2',
              'peer-disabled:cursor-not-allowed peer-disabled:opacity-50',
              className,
            )}
          />
          <div
            className={cn(
              'absolute left-0.5 top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform',
              'peer-checked:translate-x-5',
            )}
          />
        </div>
      </label>
    )
  },
)

Switch.displayName = 'Switch'
