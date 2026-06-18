import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react'
import type { SelectOption } from '@/types'

export interface UseSelectListboxOptions {
  isOpen: boolean
  setIsOpen: (open: boolean) => void
  options: SelectOption[]
  value?: string
  onSelect: (option: SelectOption) => void
  disabled?: boolean
}

function findNextEnabledIndex(options: SelectOption[], start: number, direction: 1 | -1): number {
  if (!options.length) return -1
  let index = start
  for (let step = 0; step < options.length; step += 1) {
    index = (index + direction + options.length) % options.length
    if (!options[index]?.disabled) return index
  }
  return -1
}

function findInitialHighlightIndex(options: SelectOption[], value?: string): number {
  if (!options.length) return -1
  const selectedIndex = options.findIndex((option) => option.value === value && !option.disabled)
  if (selectedIndex >= 0) return selectedIndex
  return findNextEnabledIndex(options, -1, 1)
}

export function useSelectListbox({
  isOpen,
  setIsOpen,
  options,
  value,
  onSelect,
  disabled = false,
}: UseSelectListboxOptions) {
  const [highlightedIndex, setHighlightedIndex] = useState(-1)
  const optionRefs = useRef<Array<HTMLLIElement | null>>([])

  useEffect(() => {
    optionRefs.current = optionRefs.current.slice(0, options.length)
  }, [options.length])

  useEffect(() => {
    if (!isOpen) {
      setHighlightedIndex(-1)
      return
    }
    setHighlightedIndex(findInitialHighlightIndex(options, value))
  }, [isOpen, options, value])

  useEffect(() => {
    if (!isOpen || highlightedIndex < 0) return
    optionRefs.current[highlightedIndex]?.scrollIntoView({ block: 'nearest' })
  }, [highlightedIndex, isOpen])

  const selectHighlighted = useCallback(() => {
    const option = highlightedIndex >= 0 ? options[highlightedIndex] : undefined
    if (option && !option.disabled) {
      onSelect(option)
      setIsOpen(false)
    }
  }, [highlightedIndex, onSelect, options, setIsOpen])

  const handleKeyDown = useCallback((event: KeyboardEvent<HTMLElement>) => {
    if (disabled) return

    if (!isOpen) {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter' || event.key === ' ') {
        event.preventDefault()
        setIsOpen(true)
      }
      return
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        setHighlightedIndex((current) => findNextEnabledIndex(options, current < 0 ? -1 : current, 1))
        break
      case 'ArrowUp':
        event.preventDefault()
        setHighlightedIndex((current) => findNextEnabledIndex(options, current < 0 ? options.length : current, -1))
        break
      case 'Home':
        event.preventDefault()
        setHighlightedIndex(findNextEnabledIndex(options, -1, 1))
        break
      case 'End':
        event.preventDefault()
        setHighlightedIndex(findNextEnabledIndex(options, 0, -1))
        break
      case 'Enter':
        event.preventDefault()
        selectHighlighted()
        break
      case 'Escape':
        event.preventDefault()
        setIsOpen(false)
        break
      case 'Tab':
        setIsOpen(false)
        break
      default:
        break
    }
  }, [disabled, isOpen, options, selectHighlighted, setIsOpen])

  const getOptionRef = useCallback((index: number) => (element: HTMLLIElement | null) => {
    optionRefs.current[index] = element
  }, [])

  return {
    highlightedIndex,
    handleKeyDown,
    getOptionRef,
    setHighlightedIndex,
  }
}

export function getSelectDisplayLabel(
  value: string | undefined,
  options: SelectOption[],
  selectedLabel?: string,
): string | undefined {
  if (selectedLabel) return selectedLabel
  const match = options.find((option) => option.value === value)
  if (match?.label) return match.label
  return value || undefined
}
