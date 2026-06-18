import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { ChevronDown, Loader2 } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import { useDebouncedValue } from '@/helpers/hooks/useDebouncedValue'
import { getSelectDisplayLabel, useSelectListbox } from '@/helpers/hooks/useSelectListbox'
import { resolveSelectOptionByCode } from '@/helpers/masterLookup'
import type { SelectOption } from '@/types'

export interface SearchableSelectProps {
  value?: string
  onChange?: (value: string, option?: SelectOption) => void
  onSearch: (search: string) => Promise<SelectOption[]>
  label?: string
  placeholder?: string
  searchPlaceholder?: string
  error?: string
  disabled?: boolean
  required?: boolean
  className?: string
  debounceMs?: number
  minSearchLength?: number
  selectedLabel?: string
  lookupKind?: 'item' | 'project' | 'businessPartner'
}

function mergeOptions(existing: SelectOption[], incoming: SelectOption[]): SelectOption[] {
  const map = new Map<string, SelectOption>()
  for (const option of [...incoming, ...existing]) {
    if (option.value) map.set(option.value, option)
  }
  return [...map.values()]
}

export function SearchableSelect({
  value,
  onChange,
  onSearch,
  label,
  placeholder = 'Select an option',
  searchPlaceholder = 'Type to search...',
  error,
  disabled = false,
  required = false,
  className,
  debounceMs = 300,
  minSearchLength = 0,
  selectedLabel,
  lookupKind,
}: SearchableSelectProps) {
  const id = useId()
  const listboxId = `${id}-listbox`
  const containerRef = useRef<HTMLDivElement>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [search, setSearch] = useState('')
  const [options, setOptions] = useState<SelectOption[]>([])
  const [loading, setLoading] = useState(false)
  const [fetchError, setFetchError] = useState<string | null>(null)
  const [resolvedOptions, setResolvedOptions] = useState<SelectOption[]>([])
  const debouncedSearch = useDebouncedValue(search, debounceMs)

  const allOptions = mergeOptions(options, resolvedOptions)
  const selectedOption = allOptions.find((opt) => opt.value === value)
  const displayLabel = getSelectDisplayLabel(value, allOptions, selectedLabel ?? selectedOption?.label)
  const hasValue = Boolean(value && displayLabel)

  const handleSelect = useCallback((option: SelectOption) => {
    onChange?.(option.value, option)
    setSearch('')
  }, [onChange])

  const { highlightedIndex, handleKeyDown, getOptionRef, setHighlightedIndex } = useSelectListbox({
    isOpen,
    setIsOpen,
    options: allOptions,
    value,
    onSelect: handleSelect,
    disabled,
  })

  const loadOptions = useCallback(async (term: string) => {
    if (term.trim().length < minSearchLength) {
      setOptions([])
      return
    }
    setLoading(true)
    setFetchError(null)
    try {
      const results = await onSearch(term)
      setOptions(results)
    } catch (err) {
      setFetchError(err instanceof Error ? err.message : 'Search failed')
      setOptions([])
    } finally {
      setLoading(false)
    }
  }, [minSearchLength, onSearch])

  useEffect(() => {
    if (!isOpen) return
    void loadOptions(debouncedSearch)
  }, [debouncedSearch, isOpen, loadOptions])

  useEffect(() => {
    if (!value || selectedLabel) return

    let cancelled = false
    void (async () => {
      try {
        if (lookupKind) {
          const option = await resolveSelectOptionByCode(value, lookupKind)
          if (cancelled || !option) return
          setResolvedOptions((prev) => mergeOptions(prev, [option]))
          return
        }

        const byValue = await onSearch(value)
        if (cancelled) return
        const match = byValue.find((option) => option.value === value)
        if (match) setResolvedOptions((prev) => mergeOptions(prev, [match]))
      } catch {
        // Keep showing the raw value if lookup fails.
      }
    })()

    return () => {
      cancelled = true
    }
  }, [value, selectedLabel, lookupKind, onSearch])

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  useEffect(() => {
    if (!isOpen) setSearch('')
  }, [isOpen])

  return (
    <div className={cn('w-full', className)} ref={containerRef} onKeyDown={handleKeyDown}>
      {label && (
        <label id={`${id}-label`} htmlFor={id} className="mb-1.5 block text-sm font-medium text-slate-700">
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
          aria-controls={listboxId}
          aria-labelledby={label ? `${id}-label` : undefined}
          aria-activedescendant={
            isOpen && highlightedIndex >= 0 ? `${id}-option-${highlightedIndex}` : undefined
          }
          disabled={disabled}
          onClick={() => !disabled && setIsOpen((open) => !open)}
          className={cn(
            'flex w-full items-center justify-between rounded-lg border bg-white px-3 py-2 text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-0',
            'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500',
            error
              ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
              : 'border-slate-300 focus:border-primary-500 focus:ring-primary-500',
            !hasValue && 'text-slate-400',
          )}
        >
          <span className="truncate">{displayLabel ?? placeholder}</span>
          <ChevronDown className={cn('h-4 w-4 text-slate-400 transition-transform', isOpen && 'rotate-180')} />
        </button>

        {isOpen && (
          <div className="absolute z-50 mt-1 w-full rounded-lg border border-slate-200 bg-white shadow-lg">
            <div className="border-b border-slate-100 p-2">
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={searchPlaceholder}
                className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm focus:border-primary-500 focus:outline-none focus:ring-1 focus:ring-primary-500"
                autoFocus
                aria-controls={listboxId}
                aria-autocomplete="list"
              />
            </div>
            <ul id={listboxId} className="max-h-56 overflow-auto py-1" role="listbox">
              {loading && (
                <li className="flex items-center gap-2 px-3 py-2 text-sm text-slate-500">
                  <Loader2 className="h-4 w-4 animate-spin" /> Searching...
                </li>
              )}
              {!loading && fetchError && (
                <li className="px-3 py-2 text-sm text-red-600">{fetchError}</li>
              )}
              {!loading && !fetchError && allOptions.length === 0 && (
                <li className="px-3 py-2 text-sm text-slate-500">
                  {debouncedSearch.trim().length < minSearchLength
                    ? `Type at least ${minSearchLength} characters`
                    : 'No results found'}
                </li>
              )}
              {!loading && allOptions.map((option, index) => {
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
                      handleSelect(option)
                      setIsOpen(false)
                    }}
                    className={cn(
                      'cursor-pointer px-3 py-2 text-sm',
                      isSelected && 'bg-primary-50 text-primary-700',
                      !isSelected && isHighlighted && 'bg-slate-100 text-slate-900',
                      !isSelected && !isHighlighted && 'hover:bg-slate-50',
                    )}
                  >
                    {option.label}
                  </li>
                )
              })}
            </ul>
          </div>
        )}
      </div>
      {error && <p className="mt-1.5 text-sm text-red-600">{error}</p>}
    </div>
  )
}
