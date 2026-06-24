import { Outlet, Link } from 'react-router-dom'
import { APP_TAGLINE, ROUTES } from '@/config/constants'
import { ConnectEdgeLogo } from '@/Components/brand/ConnectEdgeLogo'

export function AuthLayout() {
  return (
    <div className="flex min-h-screen">
      <div className="relative hidden w-1/2 flex-col justify-between overflow-hidden bg-sidebar p-12 text-white lg:flex">
        <div className="pointer-events-none absolute -right-24 -top-24 h-96 w-96 rounded-full bg-cyan-500/10 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-32 -left-16 h-80 w-80 rounded-full bg-indigo-500/10 blur-3xl" />

        <div className="relative">
          <ConnectEdgeLogo textClassName="text-2xl" />
          <p className="mt-3 text-primary-200">{APP_TAGLINE}</p>
        </div>

        <div className="relative">
          <blockquote className="text-lg leading-relaxed text-slate-300">
            &ldquo;Bridge your business systems with a modern, secure, and scalable
            integration platform.&rdquo;
          </blockquote>
          <p className="mt-4 text-sm text-slate-500">— ConnectEdge Team</p>
        </div>

        <p className="relative text-sm text-slate-500">
          &copy; {new Date().getFullYear()} ConnectEdge. All rights reserved.
        </p>
      </div>

      <div className="flex w-full flex-col items-center justify-center bg-slate-50 px-4 py-12 lg:w-1/2">
        <div className="mb-8 text-center lg:hidden">
          <Link to={ROUTES.LOGIN} className="inline-flex">
            <ConnectEdgeLogo textClassName="text-slate-900" />
          </Link>
        </div>
        <div className="w-full max-w-md">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
