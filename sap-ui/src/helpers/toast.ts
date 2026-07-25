type ToastVariant = 'error' | 'success' | 'info'

export type ToastItem = {
  id: number
  message: string
  variant: ToastVariant
}

type Listener = (toasts: ToastItem[]) => void

let nextId = 1
let toasts: ToastItem[] = []
const listeners = new Set<Listener>()

function emit() {
  for (const listener of listeners)
    listener(toasts)
}

export function subscribeToasts(listener: Listener): () => void {
  listeners.add(listener)
  listener(toasts)
  return () => {
    listeners.delete(listener)
  }
}

export function dismissToast(id: number) {
  toasts = toasts.filter((t) => t.id !== id)
  emit()
}

function pushToast(message: string, variant: ToastVariant) {
  const id = nextId++
  toasts = [...toasts, { id, message, variant }]
  emit()
  window.setTimeout(() => dismissToast(id), 8000)
}

export const toast = {
  error: (message: string) => pushToast(message, 'error'),
  success: (message: string) => pushToast(message, 'success'),
  info: (message: string) => pushToast(message, 'info'),
}
