import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { loadEnv, type Plugin } from 'vite'
import { buildCspPolicy, SECURITY_HEADERS, type CspMode } from './src/config/csp'

function formatHeadersFile(csp: string): string {
  const lines = ['/*', `  Content-Security-Policy: ${csp}`]
  for (const [name, value] of Object.entries(SECURITY_HEADERS)) {
    lines.push(`  ${name}: ${value}`)
  }
  lines.push('*/')
  return `${lines.join('\n')}\n`
}

function formatNginxSnippet(csp: string): string {
  const lines = [`add_header Content-Security-Policy "${csp}" always;`]
  for (const [name, value] of Object.entries(SECURITY_HEADERS)) {
    lines.push(`add_header ${name} "${value}" always;`)
  }
  return `# Auto-generated at build time. Source: src/config/csp.ts\n${lines.join('\n')}\n`
}

function applySecurityHeaders(
  res: { setHeader: (name: string, value: string) => void },
  mode: CspMode,
  env: Record<string, string>,
) {
  res.setHeader('Content-Security-Policy', buildCspPolicy(mode, {
    apiBaseUrl: env.VITE_API_BASE_URL,
    reportUri: env.VITE_CSP_REPORT_URI,
  }))
  for (const [name, value] of Object.entries(SECURITY_HEADERS)) {
    res.setHeader(name, value)
  }
}

export function securityHeadersPlugin(mode: 'serve' | 'all' = 'all'): Plugin {
  let env: Record<string, string> = {}
  let outDir = 'dist'

  return {
    name: 'security-headers',
    config(_, { mode: viteMode }) {
      env = loadEnv(viteMode, process.cwd(), '')
    },
    configResolved(config) {
      outDir = config.build.outDir
    },
    configureServer(server) {
      if (mode === 'all') {
        server.middlewares.use((_req, res, next) => {
          applySecurityHeaders(res, 'development', env)
          next()
        })
      }
    },
    configurePreviewServer(server) {
      server.middlewares.use((_req, res, next) => {
        applySecurityHeaders(res, 'production', env)
        next()
      })
    },
    closeBundle() {
      const csp = buildCspPolicy('production', {
        apiBaseUrl: env.VITE_API_BASE_URL,
        reportUri: env.VITE_CSP_REPORT_URI,
      })
      const outputDir = join(process.cwd(), outDir)
      mkdirSync(outputDir, { recursive: true })
      writeFileSync(join(outputDir, '_headers'), formatHeadersFile(csp))
      writeFileSync(join(outputDir, 'nginx-security-headers.conf'), formatNginxSnippet(csp))
    },
  }
}
