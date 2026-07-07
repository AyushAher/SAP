import { useCallback, useState, useRef, useId } from 'react'
import { createPortal } from 'react-dom'
import { ChevronDown, Check, X } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import { getSelectDisplayLabel, useSelectListbox } from '@/helpers/hooks/useSelectListbox'
import {
  getFloatingMenuStyle,
  useClickOutside,
  useFloatingMenuPortal,
} from '@/helpers/hooks/useFloatingMenuPortal'
import type { SelectOption } from '@/types'

export interface SelectProps {
  options: SelectOption[]
  value?: string
  onChange?: (value: string) => void
  label?: string
  placeholder?: string
  error?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  clearable?: boolean
  className?: string
  triggerClassName?: string
  menuClassName?: string
  usePortal?: boolean
  minHeight?: string
  menuMinHeight?: string
}

export function Select({
  options,
  value,
  onChange,
  label,
  placeholder = 'Select an option',
  error,
  hint,
  disabled = false,
  required = false,
  clearable = false,
  className,
  triggerClassName,
  menuClassName,
  usePortal = true,
  minHeight = 'min-h-[42px]',
  menuMinHeight = 'min-h-48',
}: SelectProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const { triggerRef, menuRef, menuPosition, updateMenuPosition } = useFloatingMenuPortal(isOpen, usePortal)
  const id = useId()
  const listboxId = `${id}-listbox`

  useClickOutside(containerRef, menuRef, () => setIsOpen(false))

  const handleSelect = useCallback((option: SelectOption) => {
    onChange?.(option.value)
  }, [onChange])

  const { highlightedIndex, handleKeyDown, getOptionRef, setHighlightedIndex } = useSelectListbox({
    isOpen,
    setIsOpen,
    options,
    value,
    onSelect: handleSelect,
    disabled,
  })

  const displayLabel = getSelectDisplayLabel(value, options)
  const hasValue = Boolean(value && displayLabel)

  const menuContent = (
    <ul
      ref={menuRef as React.RefObject<HTMLUListElement>}
      id={listboxId}
      role="listbox"
      aria-labelledby={label ? `${id}-label` : undefined}
      className={cn(
        'overflow-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg',
        menuMinHeight,
        'max-h-60',
        usePortal ? 'fixed z-[9999]' : 'absolute z-[200] mt-1 w-full',
        menuClassName,
      )}
      style={getFloatingMenuStyle(usePortal, menuPosition)}
    >
      {options.map((option, index) => {
        const isSelected = option.value === value
        const isHighlighted = index === highlightedIndex
        return (
          <li
            key={option.value}
            id={`${id}-option-${index}`}
            ref={getOptionRef(index)}
            role="option"
            aria-selected={isSelected}
            onMouseEnter={() => !option.disabled && setHighlightedIndex(index)}
            onClick={() => {
              if (!option.disabled) {
                handleSelect(option)
                setIsOpen(false)
              }
            }}
            className={cn(
              'flex cursor-pointer items-center justify-between px-3 py-2 text-sm',
              option.disabled && 'cursor-not-allowed opacity-50',
              isSelected && 'bg-primary-50 text-primary-700',
              !isSelected && isHighlighted && 'bg-slate-100 text-slate-900',
              !isSelected && !isHighlighted && 'text-slate-700 hover:bg-slate-50',
            )}
          >
            {option.label}
            {isSelected && <Check className="h-4 w-4 text-primary-600" />}
          </li>
        )
      })}
    </ul>
  )

  return (
    <div className={cn('w-full', className)} ref={containerRef} onKeyDown={handleKeyDown}>
      {label && (
        <label id={`${id}-label`} className="mb-1.5 block text-sm font-medium text-slate-700">
          {label}
          {required && <span className="ml-0.5 text-red-500">*</span>}
        </label>
      )}
      <div className="relative">
        <button
          ref={triggerRef}
          type="button"
          id={id}
          role="combobox"
          aria-expanded={isOpen}
          aria-haspopup="listbox"
          aria-controls={listboxId}
          aria-labelledby={label ? `${id}-label` : undefined}
          aria-activedescendant={
            isOpen && highlightedIndex >= 0 ? `${id}-option-${highlightedIndex}` : undefined
          }
          disabled={disabled}
          onClick={() => {
            if (disabled) return
            if (!isOpen && usePortal) updateMenuPosition()
            setIsOpen((open) => !open)
          }}
          className={cn(
            'flex w-full items-center justify-between rounded-lg border bg-white px-3 py-2 text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-0',
            minHeight,
            'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500',
            error
              ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
              : 'border-slate-300 focus:border-primary-500 focus:ring-primary-500',
            !hasValue && 'text-slate-400',
            triggerClassName,
          )}
        >
          <span className="truncate">{displayLabel ?? placeholder}</span>
          <div className="flex items-center gap-1">
            {clearable && value && (
              <span
                role="button"
                tabIndex={-1}
                onClick={(e) => {
                  e.stopPropagation()
                  onChange?.('')
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.stopPropagation()
                    e.preventDefault()
                    onChange?.('')
                  }
                }}
                className="rounded p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
              >
                <X className="h-3.5 w-3.5" />
              </span>
            )}
            <ChevronDown
              className={cn('h-4 w-4 text-slate-400 transition-transform', isOpen && 'rotate-180')}
            />
          </div>
        </button>

        {isOpen && (usePortal && menuPosition
          ? createPortal(menuContent, document.body)
          : menuContent)}
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
