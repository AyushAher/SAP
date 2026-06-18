import { formatApprovalLabel, formatApprovalValue } from '@/helpers/approvalUtils'

function renderValue(value: unknown): React.ReactNode {
  if (value === null || value === undefined) return <span className="text-slate-400">—</span>
  if (Array.isArray(value)) {
    if (value.length === 0) return <span className="text-slate-400">No data</span>
    if (typeof value[0] === 'object' && value[0] !== null) {
      const columns = Object.keys(value[0] as Record<string, unknown>)
      return (
        <div className="overflow-x-auto">
          <table className="min-w-full text-xs">
            <thead>
              <tr className="border-b bg-slate-50">
                {columns.map((col) => (
                  <th key={col} className="px-2 py-1 text-left font-medium text-slate-600">{formatApprovalLabel(col)}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {value.map((row, idx) => (
                <tr key={idx} className="border-b">
                  {columns.map((col) => (
                    <td key={col} className="px-2 py-1">{formatApprovalValue((row as Record<string, unknown>)[col])}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )
    }
    return (
      <ul className="list-disc pl-5 text-sm">
        {value.map((item, idx) => <li key={idx}>{formatApprovalValue(item)}</li>)}
      </ul>
    )
  }
  if (typeof value === 'object') {
    return <RequestBodyViewer data={value as Record<string, unknown>} nested />
  }
  return <span className="text-sm text-slate-800">{formatApprovalValue(value)}</span>
}

export function RequestBodyViewer({
  data,
  nested = false,
}: {
  data: Record<string, unknown>
  nested?: boolean
}) {
  return (
    <div className={nested ? 'space-y-2' : 'rounded-lg border border-slate-200 bg-white p-3'}>
      {Object.entries(data).map(([key, value]) => (
        <div key={key} className="grid gap-2 border-b border-dashed border-slate-100 py-2 last:border-0 md:grid-cols-[1fr_2fr]">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{formatApprovalLabel(key)}</div>
          <div>{renderValue(value)}</div>
        </div>
      ))}
    </div>
  )
}
