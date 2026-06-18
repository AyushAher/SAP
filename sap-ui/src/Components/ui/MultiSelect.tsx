import { useState, useRef, useEffect, useId } from 'react'
import { ChevronDown, X, Check } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import type { SelectOption } from '@/types'

export interface MultiSelectProps {
  options: SelectOption[]
  value?: string[]
  onChange?: (value: string[]) => void
  label?: string
  placeholder?: string
  error?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  maxDisplay?: number
  className?: string
}

export function MultiSelect({
  options,
  value = [],
  onChange,
  label,
  placeholder = 'Select options',
  error,
  hint,
  disabled = false,
  required = false,
  maxDisplay = 2,
  className,
}: MultiSelectProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const id = useId()

  const selectedOptions = options.filter((opt) => value.includes(opt.value))

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const toggleOption = (optionValue: string) => {
    const newValue = value.includes(optionValue)
      ? value.filter((v) => v !== optionValue)
      : [...value, optionValue]
    onChange?.(newValue)
  }

  const removeOption = (optionValue: string, e: React.MouseEvent) => {
    e.stopPropagation()
    onChange?.(value.filter((v) => v !== optionValue))
  }

  const displayText = () => {
    if (selectedOptions.length === 0) return placeholder
    if (selectedOptions.length <= maxDisplay) {
      return selectedOptions.map((o) => o.label).join(', ')
    }
    return `${selectedOptions.length} selected`
  }

  return (
    <div className={cn('w-full', className)} ref={containerRef}>
      {label && (
        <label id={`${id}-label`} className="mb-1.5 block text-sm font-medium text-slate-700">
          {label}
          {required && <span className="ml-0.5 text-red-500">*</span>}
        </label>
      )}
      <div className="relative">
        <button
          type="button"
          id={id}
          role="combobox"
          aria-expanded={isOpen}
          aria-haspopup="listbox"
          disabled={disabled}
          onClick={() => !disabled && setIsOpen(!isOpen)}
          className={cn(
            'flex w-full items-center justify-between rounded-lg border bg-white px-3 py-2 text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-0 min-h-[38px]',
            'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500',
            error
              ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
              : 'border-slate-300 focus:border-primary-500 focus:ring-primary-500',
            selectedOptions.length === 0 && 'text-slate-400',
          )}
        >
          <div className="flex flex-1 flex-wrap items-center gap-1">
            {selectedOptions.length > 0 && selectedOptions.length <= maxDisplay ? (
              selectedOptions.map((opt) => (
                <span
                  key={opt.value}
                  className="inline-flex items-center gap-1 rounded bg-primary-100 px-2 py-0.5 text-xs font-medium text-primary-700"
                >
                  {opt.label}
                  <span
                    role="button"
                    tabIndex={0}
                    onClick={(e) => removeOption(opt.value, e)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') removeOption(opt.value, e as unknown as React.MouseEvent)
                    }}
                    className="rounded hover:bg-primary-200"
                  >
                    <X className="h-3 w-3" />
                  </span>
                </span>
              ))
            ) : (
              <span className="truncate">{displayText()}</span>
            )}
          </div>
          <ChevronDown
            className={cn('ml-2 h-4 w-4 shrink-0 text-slate-400 transition-transform', isOpen && 'rotate-180')}
          />
        </button>

        {isOpen && (
          <ul
            role="listbox"
            aria-multiselectable
            className="absolute z-50 mt-1 max-h-60 w-full overflow-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg"
          >
            {options.map((option) => {
              const isSelected = value.includes(option.value)
              return (
                <li
                  key={option.value}
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => !option.disabled && toggleOption(option.value)}
                  className={cn(
                    'flex cursor-pointer items-center gap-3 px-3 py-2 text-sm',
                    option.disabled && 'cursor-not-allowed opacity-50',
                    isSelected ? 'bg-primary-50 text-primary-700' : 'text-slate-700 hover:bg-slate-50',
                  )}
                >
                  <div
                    className={cn(
                      'flex h-4 w-4 items-center justify-center rounded border',
                      isSelected ? 'border-primary-600 bg-primary-600' : 'border-slate-300',
                    )}
                  >
                    {isSelected && <Check className="h-3 w-3 text-white" strokeWidth={3} />}
                  </div>
                  {option.label}
                </li>
              )
            })}
          </ul>
        )}
      </div>
      {error && (
        <p className="mt-1.5 text-sm text-red-600" role="alert">
          {error}
        </p>
      )}
      {hint && !error && <p className="mt-1.5 text-sm text-slate-500">{hint}</p>}
    </div>
  )
}
