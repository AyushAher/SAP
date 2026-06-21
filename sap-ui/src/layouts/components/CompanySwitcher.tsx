import { useState } from 'react'
import { Building2 } from 'lucide-react'
import { Button, Input, Modal, Select } from '@/Components/ui'
import { SAP_COMPANY_DATABASES } from '@/config/companyDb'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { clearError, switchCompany } from '@/store/slices/authSlice'

export function CompanySwitcher() {
  const dispatch = useAppDispatch()
  const { companyDb, isLoading, error } = useAppSelector((state) => state.auth)
  const [pendingCompanyDb, setPendingCompanyDb] = useState<string | null>(null)
  const [password, setPassword] = useState('')

  const currentValue = companyDb ?? SAP_COMPANY_DATABASES[1].value

  const handleSelectChange = (value: string) => {
    if (value === currentValue) return
    dispatch(clearError())
    setPassword('')
    setPendingCompanyDb(value)
  }

  const handleConfirmSwitch = async () => {
    if (!pendingCompanyDb || !password) return
    const result = await dispatch(switchCompany({ companyDb: pendingCompanyDb, password }))
    if (switchCompany.fulfilled.match(result)) {
      setPendingCompanyDb(null)
      setPassword('')
    }
  }

  const handleClose = () => {
    setPendingCompanyDb(null)
    setPassword('')
    dispatch(clearError())
  }

  return (
    <>
      <div className="hidden min-w-[180px] md:block">
        <Select
          options={[...SAP_COMPANY_DATABASES]}
          value={currentValue}
          onChange={handleSelectChange}
          placeholder="Company DB"
        />
      </div>
      <div className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1.5 text-xs font-medium text-slate-600 md:hidden">
        <Building2 className="h-3.5 w-3.5" />
        {SAP_COMPANY_DATABASES.find((x) => x.value === currentValue)?.label ?? currentValue}
      </div>

      <Modal
        isOpen={pendingCompanyDb !== null}
        onClose={handleClose}
        title="Switch SAP Company Database"
        description="Enter your password to authenticate with the selected company database. Your current SAP session will be cleared."
      >
        <div className="space-y-4">
          <div className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">
            Switching to{' '}
            <span className="font-medium">
              {SAP_COMPANY_DATABASES.find((x) => x.value === pendingCompanyDb)?.label ?? pendingCompanyDb}
            </span>
          </div>
          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </div>
          )}
          <Input
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Enter your password"
            required
            autoComplete="current-password"
          />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={handleClose}>
              Cancel
            </Button>
            <Button
              type="button"
              onClick={handleConfirmSwitch}
              isLoading={isLoading}
              disabled={!password}
            >
              Switch company
            </Button>
          </div>
        </div>
      </Modal>
    </>
  )
}
