import { forwardRef, type InputHTMLAttributes, type ChangeEvent, type KeyboardEvent } from 'react'
import { cn } from '@/helpers/lib/utils'
import { isNegativeAmountInputKey, sanitizeNonNegativeAmountInput } from '@/helpers/lib/numericInput'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
  hint?: string
  leftIcon?: React.ReactNode
  rightIcon?: React.ReactNode
  nonNegative?: boolean
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({
    className,
    label,
    error,
    hint,
    leftIcon,
    rightIcon,
    id,
    required,
    nonNegative = false,
    type,
    min,
    onChange,
    onKeyDown,
    ...props
  }, ref) => {
    const inputId = id ?? label?.toLowerCase().replace(/\s+/g, '-')

    const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
      if (nonNegative) {
        const sanitized = sanitizeNonNegativeAmountInput(event.target.value)
        if (sanitized !== event.target.value) {
          onChange?.({
            ...event,
            target: { ...event.target, value: sanitized },
            currentTarget: { ...event.currentTarget, value: sanitized },
          })
          return
        }
      }
      onChange?.(event)
    }

    const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
      if (nonNegative && isNegativeAmountInputKey(event.key)) {
        event.preventDefault()
      }
      onKeyDown?.(event)
    }

    return (
      <div className="w-full">
        {label && (
          <label htmlFor={inputId} className="mb-1.5 block text-sm font-medium text-slate-700">
            {label}
            {required && <span className="ml-0.5 text-red-500">*</span>}
          </label>
        )}
        <div className="relative">
          {leftIcon && (
            <div className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
              {leftIcon}
            </div>
          )}
          <input
            ref={ref}
            id={inputId}
            type={type}
            min={nonNegative ? (min ?? '0') : min}
            className={cn(
              'block w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900',
              'placeholder:text-slate-400',
              'focus:outline-none focus:ring-2 focus:ring-offset-0',
              'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500',
              error
                ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                : 'border-slate-300 focus:border-primary-500 focus:ring-primary-500',
              leftIcon && 'pl-10',
              rightIcon && 'pr-10',
              className,
            )}
            aria-invalid={!!error}
            aria-describedby={error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined}
            required={required}
            onChange={handleChange}
            onKeyDown={handleKeyDown}
            {...props}
          />
          {rightIcon && (
            <div className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400">
              {rightIcon}
            </div>
          )}
        </div>
        {error && (
          <p id={`${inputId}-error`} className="mt-1.5 text-sm text-red-600" role="alert">
            {error}
          </p>
        )}
        {hint && !error && (
          <p id={`${inputId}-hint`} className="mt-1.5 text-sm text-slate-500">
            {hint}
          </p>
        )}
      </div>
    )
  },
)

Input.displayName = 'Input'
