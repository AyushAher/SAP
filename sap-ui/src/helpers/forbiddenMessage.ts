const FORBIDDEN_MESSAGE_KEY = 'sap_forbidden_message'

export function readForbiddenMessage(): string | null {
  try {
    return sessionStorage.getItem(FORBIDDEN_MESSAGE_KEY)
  } catch {
    return null
  }
}

export function storeForbiddenMessage(message: string): void {
  try {
    sessionStorage.setItem(FORBIDDEN_MESSAGE_KEY, message)
  } catch {
    /* ignore quota / private mode */
  }
}

export function clearForbiddenMessage(): void {
  try {
    sessionStorage.removeItem(FORBIDDEN_MESSAGE_KEY)
  } catch {
    /* ignore */
  }
}
