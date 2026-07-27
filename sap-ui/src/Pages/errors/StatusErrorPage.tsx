import { Link } from 'react-router-dom'
import { ArrowLeft, Home, type LucideIcon } from 'lucide-react'
import { Button } from '@/Components/ui'
import { ConnectEdgeLogo } from '@/Components/brand/ConnectEdgeLogo'
import { ROUTES } from '@/config/constants'

export interface StatusErrorPageProps {
  code: string
  title: string
  description: string
  icon: LucideIcon
  primaryTo?: string
  primaryLabel?: string
  secondaryTo?: string
  secondaryLabel?: string
}

export function StatusErrorPage({
  code,
  title,
  description,
  icon: Icon,
  primaryTo = ROUTES.HOME,
  primaryLabel = 'Back to dashboard',
  secondaryTo,
  secondaryLabel,
}: StatusErrorPageProps) {
  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <header className="border-b border-slate-200 bg-white px-6 py-4">
        <Link to={ROUTES.HOME} className="inline-flex">
          <ConnectEdgeLogo textClassName="text-slate-900" />
        </Link>
      </header>

      <main className="flex flex-1 items-center justify-center px-4 py-16">
        <div className="w-full max-w-lg text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl border border-slate-200 bg-white text-slate-700 shadow-sm">
            <Icon className="h-8 w-8" aria-hidden />
          </div>
          <p className="mt-6 text-sm font-semibold tracking-wide text-primary-600">{code}</p>
          <h1 className="mt-2 text-3xl font-bold text-slate-900">{title}</h1>
          <p className="mt-3 text-sm leading-relaxed text-slate-500">{description}</p>

          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link to={primaryTo}>
              <Button leftIcon={<Home className="h-4 w-4" />}>{primaryLabel}</Button>
            </Link>
            {secondaryTo && secondaryLabel && (
              <Link to={secondaryTo}>
                <Button variant="outline" leftIcon={<ArrowLeft className="h-4 w-4" />}>
                  {secondaryLabel}
                </Button>
              </Link>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}
