import { Modal } from '@/Components/ui'
import type { StageWisePaymentSummaryItem } from '@/Requests/approvals'

interface StageWisePaymentSummaryDialogProps {
  isOpen: boolean
  onClose: () => void
  rows: StageWisePaymentSummaryItem[]
}

export function StageWisePaymentSummaryDialog({ isOpen, onClose, rows }: StageWisePaymentSummaryDialogProps) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Payment Summary" size="2xl">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b bg-slate-50 text-left text-xs uppercase text-slate-500">
              <th className="px-3 py-2">Request ID</th>
              <th className="px-3 py-2 text-right">Payment Stage</th>
              <th className="px-3 py-2 text-right">Net Basic</th>
              <th className="px-3 py-2 text-right">TDS</th>
              <th className="px-3 py-2 text-right">GST</th>
              <th className="px-3 py-2 text-right">Gross</th>
              <th className="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, idx) => (
              <tr key={idx} className={row.isTotalRow ? 'border-t-2 font-semibold' : 'border-b'}>
                <td className="px-3 py-2">{row.requestId}</td>
                <td className="px-3 py-2 text-right">{row.paymentStage}</td>
                <td className="px-3 py-2 text-right">{row.netBasicAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</td>
                <td className="px-3 py-2 text-right">{row.tdsAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</td>
                <td className="px-3 py-2 text-right">{row.gstAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</td>
                <td className="px-3 py-2 text-right">{row.grossAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</td>
                <td className="px-3 py-2">{row.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Modal>
  )
}
