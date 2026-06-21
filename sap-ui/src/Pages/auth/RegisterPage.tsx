import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Mail, Lock, User } from 'lucide-react'
import { Button, Input, Select } from '@/Components/ui'
import { DEFAULT_COMPANY_DB, SAP_COMPANY_DATABASES } from '@/config/companyDb'
import { ROUTES } from '@/config/constants'
import { registerApi } from '@/Requests/auth'

export function RegisterPage() {
  const navigate = useNavigate()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [companyDb, setCompanyDb] = useState<string>(DEFAULT_COMPANY_DB)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await registerApi({
        fullName,
        userName,
        email,
        password,
        companyDb,
      })
      navigate(ROUTES.LOGIN)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <h2 className="text-2xl font-bold text-slate-900">Create an account</h2>
      <p className="mt-2 text-sm text-slate-500">
        Register with your SAP credentials to access the platform
      </p>

      <form onSubmit={handleSubmit} className="mt-8 space-y-5">
        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
        )}
        <Select
          label="SAP Company Database"
          options={[...SAP_COMPANY_DATABASES]}
          value={companyDb}
          onChange={setCompanyDb}
          required
        />
        <Input
          label="Full Name"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          placeholder="John Doe"
          leftIcon={<User className="h-4 w-4" />}
          required
          autoComplete="name"
        />
        <Input
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@company.com"
          leftIcon={<Mail className="h-4 w-4" />}
          required
          autoComplete="email"
        />
        <Input
          label="SAP Username"
          type="text"
          value={userName}
          onChange={(e) => setUserName(e.target.value)}
          placeholder="Enter your SAP username"
          leftIcon={<User className="h-4 w-4" />}
          hint="Used to sign in and authenticate with SAP"
          required
          autoComplete="username"
        />
        <Input
          label="Password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Enter your SAP password"
          leftIcon={<Lock className="h-4 w-4" />}
          required
          autoComplete="new-password"
        />
        <Button type="submit" className="w-full" size="lg" isLoading={loading}>
          Create account
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-slate-500">
        Already have an account?{' '}
        <Link to={ROUTES.LOGIN} className="font-medium text-primary-600 hover:text-primary-700">
          Sign in
        </Link>
      </p>
    </div>
  )
}
