import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Loader2 } from 'lucide-react'
import { cn } from '@/helpers/lib/utils'

export interface BlockingLoaderProps {
  visible: boolean
  label?: string
  className?: string
  /** When false, shows the overlay without locking page scroll. */
  lockScroll?: boolean
}

export function BlockingLoader({ visible, label = 'Loading', className, lockScroll = true }: BlockingLoaderProps) {
  useEffect(() => {
    if (!visible || !lockScroll) return

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [visible, lockScroll])

  if (!visible) return null

  return createPortal(
    <div
      className={cn(
        'fixed inset-0 z-[10000] flex items-center justify-center bg-slate-900/40 backdrop-blur-[1px]',
        className,
      )}
      aria-busy="true"
      aria-live="polite"
      role="presentation"
    >
      <div
        className="flex flex-col items-center gap-3 rounded-xl bg-white px-8 py-6 shadow-xl"
        role="status"
        aria-label={label}
      >
        <Loader2 className="h-8 w-8 animate-spin text-primary-600" aria-hidden="true" />
        {label && <p className="text-sm font-medium text-slate-700">{label}</p>}
      </div>
    </div>,
    document.body,
  )
}
