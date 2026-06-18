/**
 * Content-Security-Policy builder for SAP UI.
 *
 * Production policy is strict: self-hosted assets only, no external scripts.
 * Development relaxes script-src (Vite HMR) and connect-src (dev server + API proxy).
 */

export type CspMode = 'production' | 'development'

function parseConnectOrigins(apiBaseUrl: string | undefined): string[] {
  const origins = new Set<string>(["'self'"])

  if (!apiBaseUrl || apiBaseUrl.startsWith('/')) {
    return [...origins]
  }

  try {
    origins.add(new URL(apiBaseUrl).origin)
  } catch {
    // Relative or invalid URL — same-origin only
  }

  return [...origins]
}

function joinDirective(name: string, values: string[]): string {
  return `${name} ${values.join(' ')}`
}

export function buildCspPolicy(mode: CspMode, options: {
  apiBaseUrl?: string
  reportUri?: string
  extraConnectSrc?: string[]
} = {}): string {
  const apiBaseUrl = options.apiBaseUrl
  const reportUri = options.reportUri
  const connectSrc = [
    ...parseConnectOrigins(apiBaseUrl),
    ...(options?.extraConnectSrc ?? []),
  ]

  if (mode === 'development') {
    connectSrc.push('ws:', 'wss:', 'http://localhost:5033')
  }

  const directives: string[] = [
    joinDirective('default-src', ["'self'"]),
    joinDirective(
      'script-src',
      mode === 'development'
        // Vite HMR + @vitejs/plugin-react Fast Refresh inject inline scripts in dev only
        ? ["'self'", "'unsafe-eval'", "'unsafe-inline'"]
        : ["'self'"],
    ),
    // React inline styles + ECharts canvas sizing require unsafe-inline
    joinDirective('style-src', ["'self'", "'unsafe-inline'"]),
    joinDirective('font-src', ["'self'"]),
    joinDirective('img-src', ["'self'", 'data:', 'blob:']),
    joinDirective('connect-src', connectSrc),
    joinDirective('worker-src', ["'self'", 'blob:']),
    joinDirective('object-src', ["'none'"]),
    joinDirective('base-uri', ["'self'"]),
    joinDirective('form-action', ["'self'"]),
    joinDirective('frame-ancestors', ["'none'"]),
  ]

  // Local dev runs over http://localhost — do not force HTTPS upgrades
  if (mode === 'production') {
    directives.push('upgrade-insecure-requests')
  }

  if (reportUri) {
    directives.push(joinDirective('report-uri', [reportUri]))
  }

  return directives.join('; ')
}

export const SECURITY_HEADERS: Record<string, string> = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'camera=(), microphone=(), geolocation=(), payment=()',
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Resource-Policy': 'same-origin',
}
