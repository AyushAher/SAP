type ApiLoadingListener = (activeRequests: number) => void

let activeRequests = 0
const listeners = new Set<ApiLoadingListener>()

function notifyListeners() {
  listeners.forEach((listener) => listener(activeRequests))
}

export function getApiLoadingCount(): number {
  return activeRequests
}

export function subscribeApiLoading(listener: ApiLoadingListener): () => void {
  listeners.add(listener)
  listener(activeRequests)
  return () => listeners.delete(listener)
}

export function incrementApiLoading(): void {
  activeRequests += 1
  notifyListeners()
}

export function decrementApiLoading(): void {
  activeRequests = Math.max(0, activeRequests - 1)
  notifyListeners()
}
