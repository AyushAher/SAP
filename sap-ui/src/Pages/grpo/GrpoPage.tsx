import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, Card, CardContent, Input } from '@/Components/ui'
import { ROUTES } from '@/config/constants'
import { createGrpo, getGrpoFromPo } from '@/Requests/grpo'

export function GrpoPage() {
  const [poDocEntry, setPoDocEntry] = useState('')
  const [data, setData] = useState<Record<string, unknown> | null>(null)
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const loadFromPo = async () => {
    if (!poDocEntry) return
    setLoading(true)
    setMessage(null)
    try {
      const res = await getGrpoFromPo(Number(poDocEntry))
      setData(res)
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed to load PO')
    } finally {
      setLoading(false)
    }
  }

  const submit = async () => {
    if (!data) return
    setLoading(true)
    try {
      await createGrpo(data)
      setMessage('GRPO posted to SAP successfully')
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'GRPO failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Goods Receipt PO"
        description="Create GRPO from purchase order"
        action={<Link to={ROUTES.PURCHASE_ORDERS}><Button variant="outline">Back to PO</Button></Link>}
      />
      {message && <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm">{message}</div>}
      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 pt-6">
          <Input label="PO Doc Entry" value={poDocEntry} onChange={(e) => setPoDocEntry(e.target.value)} />
          <Button onClick={loadFromPo} isLoading={loading}>Load from PO</Button>
          {data && <Button onClick={submit} isLoading={loading}>Post GRPO to SAP</Button>}
        </CardContent>
      </Card>
      {data && (
        <Card>
          <CardContent className="pt-6">
            <pre className="max-h-96 overflow-auto rounded bg-slate-100 p-4 text-xs">{JSON.stringify(data, null, 2)}</pre>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
