import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/helpers/lib/utils'
import { Check } from 'lucide-react'

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string
  description?: string
  error?: string
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, label, description, error, id, ...props }, ref) => {
    const inputId = id ?? label?.toLowerCase().replace(/\s+/g, '-')

    return (
      <div>
        <label htmlFor={inputId} className="group flex cursor-pointer items-start gap-3">
          <div className="relative flex items-center">
            <input
              ref={ref}
              type="checkbox"
              id={inputId}
              className={cn('sr-only', className)}
              {...props}
            />
            <div
              className={cn(
                'flex h-5 w-5 items-center justify-center rounded border-2 transition-colors',
                'group-has-[:checked]:border-primary-600 group-has-[:checked]:bg-primary-600',
                'group-has-[:focus-visible]:ring-2 group-has-[:focus-visible]:ring-primary-500 group-has-[:focus-visible]:ring-offset-2',
                'group-has-[:disabled]:cursor-not-allowed group-has-[:disabled]:opacity-50',
                error ? 'border-red-300' : 'border-slate-300',
              )}
            >
              <Check
                className="h-3 w-3 text-white opacity-0 group-has-[:checked]:opacity-100"
                strokeWidth={3}
              />
            </div>
          </div>
          {(label || description) && (
            <div className="flex-1">
              {label && <span className="text-sm font-medium text-slate-700">{label}</span>}
              {description && <p className="text-sm text-slate-500">{description}</p>}
            </div>
          )}
        </label>
        {error && (
          <p className="mt-1.5 text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
      </div>
    )
  },
)

Checkbox.displayName = 'Checkbox'
