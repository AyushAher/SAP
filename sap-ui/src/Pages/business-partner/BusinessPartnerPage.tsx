import { useState, type FormEvent } from 'react'
import { PageHeader } from '@/Components/shared/PageHeader'
import { Button, Card, CardContent, Input } from '@/Components/ui'
import { apiPost } from '@/helpers/api/client'

export function BusinessPartnerPage() {
  const [form, setForm] = useState({ CardCode: '', CardName: '', CardType: 'cSupplier' })
  const [message, setMessage] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await apiPost('/business-partner', form)
      setMessage('Business partner saved to SAP successfully')
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Failed')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Business Partner" description="Sync vendor/customer to SAP" />
      {message && <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm">{message}</div>}
      <Card><CardContent className="pt-6">
        <form onSubmit={handleSubmit} className="grid max-w-lg gap-4">
          <Input label="Card Code" value={form.CardCode} onChange={(e) => setForm({ ...form, CardCode: e.target.value })} required />
          <Input label="Card Name" value={form.CardName} onChange={(e) => setForm({ ...form, CardName: e.target.value })} required />
          <div>
            <label className="mb-1 block text-sm font-medium">Card Type</label>
            <select className="w-full rounded-lg border px-3 py-2" value={form.CardType} onChange={(e) => setForm({ ...form, CardType: e.target.value })}>
              <option value="cSupplier">Vendor</option>
              <option value="cCustomer">Customer</option>
            </select>
          </div>
          <Button type="submit" isLoading={saving}>Save to SAP</Button>
        </form>
      </CardContent></Card>
    </div>
  )
}
