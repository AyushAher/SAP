import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'

export interface FloatingMenuPosition {
  top: number
  left: number
  width: number
}

export function useFloatingMenuPortal(isOpen: boolean, usePortal: boolean) {
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLElement>(null)
  const [menuPosition, setMenuPosition] = useState<FloatingMenuPosition | null>(null)

  const updateMenuPosition = useCallback(() => {
    if (!triggerRef.current) return
    const rect = triggerRef.current.getBoundingClientRect()
    setMenuPosition({
      top: rect.bottom + 4,
      left: rect.left,
      width: rect.width,
    })
  }, [])

  useEffect(() => {
    if (!isOpen || !usePortal) {
      setMenuPosition(null)
      return
    }
    updateMenuPosition()
    window.addEventListener('resize', updateMenuPosition)
    window.addEventListener('scroll', updateMenuPosition, true)
    return () => {
      window.removeEventListener('resize', updateMenuPosition)
      window.removeEventListener('scroll', updateMenuPosition, true)
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
    width: menuPosition.width,
    zIndex: 9999,
  }
}
