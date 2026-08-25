import { useEffect, useState } from 'react'
import { Input, type InputProps } from './Input'
import { formatPoDisplayDate, parsePoDisplayDate, toIsoDateOnly } from '@/helpers/lib/utils'

export interface SapDateInputProps extends Omit<InputProps, 'type' | 'value' | 'onChange' | 'placeholder'> {
  value?: string | Date | null
  onChangeIso: (iso: string) => void
}

/** Text date field matching PO posting date: type dd/MM/yyyy, store ISO yyyy-MM-dd. */
export function SapDateInput({ value, onChangeIso, onBlur, ...props }: SapDateInputProps) {
  const iso = toIsoDateOnly(value) ?? ''
  const [display, setDisplay] = useState(() => formatPoDisplayDate(iso))

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

  return (
    <Input
      placeholder="DD/MM/YYYY"
      {...props}
      value={display}
      onChange={(e) => setDisplay(e.target.value)}
      onBlur={(e) => {
        commit()
        onBlur?.(e)
      }}
    />
  )
}
