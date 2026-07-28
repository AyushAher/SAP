import { useCallback, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { MoreVertical } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'
import {
  getFloatingMenuStyle,
  useClickOutside,
  useFloatingMenuPortal,
} from '@/helpers/hooks/useFloatingMenuPortal'
import { rowActionIconClassName } from '@/Components/shared/RowActions'

export interface RowActionMenuItem {
  key: string
  label: string
  icon?: ReactNode
  onClick?: () => void
  to?: string
  disabled?: boolean
}

interface RowActionsMenuProps {
  items: RowActionMenuItem[]
  title?: string
}

export function RowActionsMenu({ items, title = 'Actions' }: RowActionsMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const { triggerRef, menuRef, menuPosition, updateMenuPosition } = useFloatingMenuPortal(isOpen, true)

  useClickOutside(containerRef, menuRef, () => setIsOpen(false))

  const close = useCallback(() => setIsOpen(false), [])

  const toggle = useCallback(() => {
    updateMenuPosition()
    setIsOpen((prev) => !prev)
  }, [updateMenuPosition])

  const menuContent = (
    <ul
      ref={menuRef as React.RefObject<HTMLUListElement>}
      role="menu"
      aria-label={title}
      className="fixed z-[9999] min-w-[11rem] overflow-hidden rounded-lg border border-slate-200 bg-white py-1 shadow-lg"
      style={getFloatingMenuStyle(true, menuPosition)}
    >
      {items.map((item) => {
        const content = (
          <>
            {item.icon && <span className="shrink-0 text-slate-500">{item.icon}</span>}
            <span>{item.label}</span>
          </>
        )

        const className = cn(
          'flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-slate-700 transition-colors',
          item.disabled
            ? 'cursor-not-allowed opacity-50'
            : 'hover:bg-slate-50 focus:bg-slate-50 focus:outline-none',
        )

        if (item.to && !item.disabled) {
          return (
            <li key={item.key} role="none">
              <Link to={item.to} role="menuitem" className={className} onClick={close}>
                {content}
              </Link>
            </li>
          )
        }

        return (
          <li key={item.key} role="none">
            <button
              type="button"
              role="menuitem"
              className={className}
              disabled={item.disabled}
              onClick={() => {
                if (item.disabled) return
                item.onClick?.()
                close()
              }}
            >
              {content}
            </button>
          </li>
        )
      })}
    </ul>
  )

  return (
    <div ref={containerRef} className="relative inline-flex">
      <button
        ref={triggerRef}
        type="button"
        title={title}
        aria-label={title}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        onClick={toggle}
        className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-slate-300 bg-white text-slate-600 transition-colors hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-1"
      >
        <MoreVertical className={rowActionIconClassName} />
      </button>
      {isOpen && createPortal(menuContent, document.body)}
    </div>
  )
}
