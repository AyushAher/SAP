import { useCallback, useEffect, useLayoutEffect, useRef, useState, type RefObject } from 'react'

export interface FloatingMenuPosition {
  top: number
  left: number
  width?: number
}

export interface FloatingMenuPortalOptions {
  /** Horizontal alignment relative to the trigger. Default: start (left). */
  align?: 'start' | 'end'
  /** When true, menu width matches the trigger width (selects). Default: true. */
  matchTriggerWidth?: boolean
  /** Viewport padding when clamping. Default: 8. */
  viewportPadding?: number
}

export function useFloatingMenuPortal(
  isOpen: boolean,
  usePortal: boolean,
  options: FloatingMenuPortalOptions = {},
) {
  const {
    align = 'start',
    matchTriggerWidth = true,
    viewportPadding = 8,
  } = options

  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLElement>(null)
  const [menuPosition, setMenuPosition] = useState<FloatingMenuPosition | null>(null)

  const updateMenuPosition = useCallback(() => {
    if (!triggerRef.current) return
    const rect = triggerRef.current.getBoundingClientRect()
    const menuEl = menuRef.current
    const measuredWidth = menuEl?.offsetWidth
      ?? (matchTriggerWidth ? rect.width : 176)
    const measuredHeight = menuEl?.offsetHeight ?? 0

    let left = align === 'end' ? rect.right - measuredWidth : rect.left
    left = Math.max(
      viewportPadding,
      Math.min(left, window.innerWidth - measuredWidth - viewportPadding),
    )

    let top = rect.bottom + 4
    if (measuredHeight > 0 && top + measuredHeight > window.innerHeight - viewportPadding) {
      const above = rect.top - measuredHeight - 4
      top = above >= viewportPadding ? above : Math.max(viewportPadding, window.innerHeight - measuredHeight - viewportPadding)
    }

    setMenuPosition((prev) => {
      const next: FloatingMenuPosition = {
        top,
        left,
        width: matchTriggerWidth ? rect.width : undefined,
      }
      if (
        prev
        && prev.top === next.top
        && prev.left === next.left
        && prev.width === next.width
      ) {
        return prev
      }
      return next
    })
  }, [align, matchTriggerWidth, viewportPadding])

  useLayoutEffect(() => {
    if (!isOpen || !usePortal) return
    updateMenuPosition()
  }, [isOpen, usePortal, updateMenuPosition])

  useEffect(() => {
    if (!isOpen || !usePortal) {
      setMenuPosition(null)
      return
    }
    updateMenuPosition()

    const handleScroll = (event: Event) => {
      // Ignore scrolls inside the dropdown itself — those must not reposition the menu
      // (repositioning re-renders and was resetting list scroll / keyboard highlight).
      const target = event.target
      if (target instanceof Node && menuRef.current?.contains(target)) return
      updateMenuPosition()
    }

    window.addEventListener('resize', updateMenuPosition)
    window.addEventListener('scroll', handleScroll, true)
    return () => {
      window.removeEventListener('resize', updateMenuPosition)
      window.removeEventListener('scroll', handleScroll, true)
    }
  }, [isOpen, usePortal, updateMenuPosition])

  return { triggerRef, menuRef, menuPosition, updateMenuPosition }
}

export function useClickOutside(
  containerRef: RefObject<HTMLElement | null>,
  menuRef: RefObject<HTMLElement | null>,
  onClose: () => void,
) {
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      const target = event.target as Node
      if (containerRef.current?.contains(target)) return
      if (menuRef.current?.contains(target)) return
      onClose()
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [containerRef, menuRef, onClose])
}

export function getFloatingMenuStyle(
  usePortal: boolean,
  menuPosition: FloatingMenuPosition | null,
): React.CSSProperties | undefined {
  if (!usePortal || !menuPosition) return undefined
  return {
    position: 'fixed',
    top: menuPosition.top,
    left: menuPosition.left,
    ...(menuPosition.width != null ? { width: menuPosition.width } : {}),
    zIndex: 9999,
  }
}
