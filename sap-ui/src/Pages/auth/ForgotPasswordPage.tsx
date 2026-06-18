import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Mail, ArrowLeft } from 'lucide-react'
import { Button, Input } from '@/Components/ui'
import { ROUTES } from '@/config/constants'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitted, setSubmitted] = useState(false)

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    setSubmitted(true)
  }

  if (submitted) {
    return (
      <div className="text-center">
        <h2 className="text-2xl font-bold text-slate-900">Reset link sent</h2>
        <p className="mt-2 text-sm text-slate-500">
          If an account exists for <strong>{email}</strong>, you will receive a password reset email.
        </p>
        <Link
          to={ROUTES.LOGIN}
          className="mt-6 inline-flex items-center gap-2 text-sm font-medium text-primary-600"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to login
        </Link>
      </div>
    )
  }

  return (
    <div>
      <h2 className="text-2xl font-bold text-slate-900">Forgot password?</h2>
      <p className="mt-2 text-sm text-slate-500">
        Enter your email and we&apos;ll send you a reset link
      </p>

      <form onSubmit={handleSubmit} className="mt-8 space-y-5">
        <Input
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@company.com"
          leftIcon={<Mail className="h-4 w-4" />}
          required
        />
        <Button type="submit" className="w-full" size="lg">
          Send reset link
        </Button>
      </form>

      <Link
        to={ROUTES.LOGIN}
        className="mt-6 inline-flex items-center gap-2 text-sm font-medium text-primary-600 hover:text-primary-700"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to login
      </Link>
    </div>
  )
}
