import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Calendar as CalendarIcon, ChevronLeft, ChevronRight } from 'lucide-react'
import { Input, type InputProps } from './Input'
import { formatPoDisplayDate, parsePoDisplayDate, toIsoDateOnly } from '@/helpers/lib/utils'
import { cn } from '@/helpers/lib/utils'
import {
  getFloatingMenuStyle,
  useClickOutside,
  useFloatingMenuPortal,
} from '@/helpers/hooks/useFloatingMenuPortal'

export interface SapDateInputProps extends Omit<InputProps, 'type' | 'value' | 'onChange' | 'placeholder'> {
  value?: string | Date | null
  onChangeIso: (iso: string) => void
}

const WEEKDAY_LABELS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa']

function todayIso(): string {
  return toIsoDateOnly(new Date())!
}

/** Days to render for a month grid, padded to full weeks with adjacent-month days. */
function buildMonthGrid(year: number, month: number): Date[] {
  const first = new Date(year, month, 1)
  const start = new Date(first)
  start.setDate(first.getDate() - first.getDay())
  const days: Date[] = []
  for (let i = 0; i < 42; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    days.push(d)
  }
  return days
}

function isoOf(date: Date): string {
  return toIsoDateOnly(date)!
}

interface CalendarPopoverProps {
  selectedIso: string
  onSelect: (iso: string) => void
}

function CalendarPopover({ selectedIso, onSelect }: CalendarPopoverProps) {
  const anchor = selectedIso ? new Date(`${selectedIso}T00:00:00`) : new Date()
  const [viewYear, setViewYear] = useState(anchor.getFullYear())
  const [viewMonth, setViewMonth] = useState(anchor.getMonth())

  const days = buildMonthGrid(viewYear, viewMonth)
  const today = todayIso()

  const goPrevMonth = () => {
    const d = new Date(viewYear, viewMonth - 1, 1)
    setViewYear(d.getFullYear())
    setViewMonth(d.getMonth())
  }
  const goNextMonth = () => {
    const d = new Date(viewYear, viewMonth + 1, 1)
    setViewYear(d.getFullYear())
    setViewMonth(d.getMonth())
  }

  return (
    <div className="w-64 p-3">
      <div className="mb-2 flex items-center justify-between">
        <button
          type="button"
          onClick={goPrevMonth}
          className="rounded p-1 text-slate-500 hover:bg-slate-100"
          aria-label="Previous month"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="text-sm font-medium text-slate-800">
          {new Date(viewYear, viewMonth, 1).toLocaleString('en-US', { month: 'long', year: 'numeric' })}
        </span>
        <button
          type="button"
          onClick={goNextMonth}
          className="rounded p-1 text-slate-500 hover:bg-slate-100"
          aria-label="Next month"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>
      <div className="grid grid-cols-7 gap-0.5 text-center text-xs text-slate-400">
        {WEEKDAY_LABELS.map((w) => <div key={w} className="py-1">{w}</div>)}
      </div>
      <div className="grid grid-cols-7 gap-0.5">
        {days.map((d) => {
          const iso = isoOf(d)
          const inMonth = d.getMonth() === viewMonth
          const isSelected = iso === selectedIso
          const isToday = iso === today
          return (
            <button
              key={iso}
              type="button"
              onClick={() => onSelect(iso)}
              className={cn(
                'h-8 rounded text-sm',
                !inMonth && 'text-slate-300',
                inMonth && !isSelected && 'text-slate-700 hover:bg-slate-100',
                isSelected && 'bg-primary-600 text-white hover:bg-primary-600',
                !isSelected && isToday && 'font-semibold text-primary-600',
              )}
            >
              {d.getDate()}
            </button>
          )
        })}
      </div>
      <div className="mt-2 flex justify-between border-t border-slate-100 pt-2">
        <button
          type="button"
          onClick={() => onSelect(today)}
          className="text-xs font-medium text-primary-600 hover:underline"
        >
          Today
        </button>
        <button
          type="button"
          onClick={() => onSelect('')}
          className="text-xs font-medium text-slate-500 hover:underline"
        >
          Clear
        </button>
      </div>
    </div>
  )
}

/** Text date field matching PO posting date: type dd/MM/yyyy, store ISO yyyy-MM-dd. Also offers a
 * calendar popover (via the trailing icon) so dates never have to be typed. */
export function SapDateInput({ value, onChangeIso, onBlur, disabled, readOnly, ...props }: SapDateInputProps) {
  const iso = toIsoDateOnly(value) ?? ''
  const [display, setDisplay] = useState(() => formatPoDisplayDate(iso))
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const { triggerRef, menuRef, menuPosition, updateMenuPosition } = useFloatingMenuPortal(isOpen, true, {
    align: 'end',
    matchTriggerWidth: false,
  })

  useClickOutside(containerRef, menuRef, () => setIsOpen(false))

  useEffect(() => {
    setDisplay(formatPoDisplayDate(iso))
  }, [iso])

  const commit = () => {
    const parsed = parsePoDisplayDate(display) ?? toIsoDateOnly(display)
    if (parsed) {
      setDisplay(formatPoDisplayDate(parsed))
      onChangeIso(parsed)
      return
    }
    if (!display.trim()) {
      setDisplay('')
      onChangeIso('')
      return
    }
    setDisplay(formatPoDisplayDate(iso))
  }

  const canOpen = !disabled && !readOnly

  return (
    <div ref={containerRef} className="relative">
      <Input
        placeholder="DD/MM/YYYY"
        {...props}
        disabled={disabled}
        readOnly={readOnly}
        value={display}
        onChange={(e) => setDisplay(e.target.value)}
        onBlur={(e) => {
          commit()
          onBlur?.(e)
        }}
        rightIcon={(
          <button
            ref={triggerRef}
            type="button"
            tabIndex={-1}
            disabled={!canOpen}
            aria-label="Open calendar"
            onClick={() => {
              if (!canOpen) return
              if (!isOpen) updateMenuPosition()
              setIsOpen((open) => !open)
            }}
            className="pointer-events-auto rounded p-0.5 text-slate-400 hover:text-slate-600 disabled:cursor-not-allowed disabled:hover:text-slate-400"
          >
            <CalendarIcon className="h-4 w-4" />
          </button>
        )}
      />
      {isOpen && menuPosition && createPortal(
        <div
          ref={menuRef as React.RefObject<HTMLDivElement>}
          className="fixed z-[9999] rounded-lg border border-slate-200 bg-white shadow-lg"
          style={getFloatingMenuStyle(true, menuPosition)}
        >
          <CalendarPopover
            selectedIso={iso}
            onSelect={(nextIso) => {
              onChangeIso(nextIso)
              setDisplay(formatPoDisplayDate(nextIso))
              setIsOpen(false)
            }}
          />
        </div>,
        document.body,
      )}
    </div>
  )
}
