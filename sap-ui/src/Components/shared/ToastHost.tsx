import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { dismissToast, subscribeToasts, type ToastItem } from '@/helpers/toast'
import { cn } from '@/helpers/lib/utils'

const variantClass: Record<ToastItem['variant'], string> = {
  error: 'border-red-200 bg-red-50 text-red-900',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  info: 'border-slate-200 bg-white text-slate-800',
}

export function ToastHost() {
  const [items, setItems] = useState<ToastItem[]>([])

  useEffect(() => subscribeToasts(setItems), [])

  if (!items.length) return null

  return (
    <div
      className="pointer-events-none fixed right-4 top-4 z-[100] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2"
      aria-live="polite"
    >
      {items.map((item) => (
        <div
          key={item.id}
          role="status"
          className={cn(
            'pointer-events-auto flex items-start gap-3 rounded-lg border px-4 py-3 text-sm shadow-md',
            variantClass[item.variant],
          )}
        >
          <p className="min-w-0 flex-1 break-words">{item.message}</p>
          <button
            type="button"
            className="shrink-0 rounded p-0.5 opacity-70 hover:opacity-100"
            aria-label="Dismiss"
            onClick={() => dismissToast(item.id)}
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      ))}
    </div>
  )
}
