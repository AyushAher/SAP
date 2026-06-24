import { useEffect, useMemo, useState } from 'react'
import { GitBranch } from 'lucide-react'
import { Select } from '@/Components/ui'
import { getBranchesApi, type BranchOption } from '@/Requests/auth'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { switchBranch } from '@/store/slices/authSlice'

const NONE_VALUE = ''

function normalizeBranch(option: BranchOption): { value: string; label: string } {
  const id = option.id ?? (option as { Id?: number }).Id
  const name = option.name ?? (option as { Name?: string }).Name ?? `Branch ${id}`
  return { value: String(id), label: name }
}

export function BranchSwitcher() {
  const dispatch = useAppDispatch()
  const { branchId, isLoading, isAuthenticated, token, companyDb } = useAppSelector((state) => state.auth)
  const [branches, setBranches] = useState<Array<{ value: string; label: string }>>([])
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    if (!isAuthenticated || !token) return

    let active = true
    getBranchesApi()
      .then((items) => {
        if (!active) return
        setBranches(items.map(normalizeBranch))
        setLoadError(null)
      })
      .catch((err: Error) => {
        if (!active) return
        setLoadError(err.message)
      })
    return () => {
      active = false
    }
  }, [isAuthenticated, token, companyDb])

  const options = useMemo(
    () => [{ value: NONE_VALUE, label: 'None' }, ...branches],
    [branches],
  )

  const currentValue = branchId === null ? NONE_VALUE : String(branchId)

  const handleSelectChange = (value: string) => {
    if (value === currentValue || isLoading) return
    const nextBranchId = value === NONE_VALUE ? null : Number(value)
    void dispatch(switchBranch(nextBranchId))
  }

  return (
    <>
      <div className="hidden min-w-[180px] md:block">
        <Select
          options={options}
          value={currentValue}
          onChange={handleSelectChange}
          placeholder="Branch"
          disabled={isLoading || Boolean(loadError)}
        />
      </div>
      <div className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1.5 text-xs font-medium text-slate-600 md:hidden">
        <GitBranch className="h-3.5 w-3.5" />
        {options.find((x) => x.value === currentValue)?.label ?? 'Branch'}
      </div>
    </>
  )
}
