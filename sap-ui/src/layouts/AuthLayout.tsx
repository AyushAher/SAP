import { Outlet, Link } from 'react-router-dom'
import { APP_NAME, ROUTES } from '@/config/constants'

export function AuthLayout() {
  return (
    <div className="flex min-h-screen">
      <div className="hidden w-1/2 flex-col justify-between bg-primary-900 p-12 text-white lg:flex">
        <div>
          <h1 className="text-3xl font-bold">{APP_NAME}</h1>
          <p className="mt-2 text-primary-200">Enterprise Application Platform</p>
        </div>
        <div>
          <blockquote className="text-lg leading-relaxed text-primary-100">
            &ldquo;Streamline your business operations with a modern, secure, and scalable
            enterprise solution.&rdquo;
          </blockquote>
          <p className="mt-4 text-sm text-primary-300">— Enterprise Team</p>
        </div>
        <p className="text-sm text-primary-400">
          &copy; {new Date().getFullYear()} {APP_NAME}. All rights reserved.
        </p>
      </div>

      <div className="flex w-full flex-col items-center justify-center bg-slate-50 px-4 py-12 lg:w-1/2">
        <div className="mb-8 text-center lg:hidden">
          <Link to={ROUTES.LOGIN} className="text-2xl font-bold text-primary-900">
            {APP_NAME}
          </Link>
        </div>
        <div className="w-full max-w-md">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
