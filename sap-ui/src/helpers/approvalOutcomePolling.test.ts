import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { pollApprovalOutcome } from '@/helpers/approvalOutcomePolling'

const toastSuccess = vi.fn()
const toastError = vi.fn()

vi.mock('@/helpers/toast', () => ({
  toast: {
    success: (message: string) => toastSuccess(message),
    error: (message: string) => toastError(message),
    info: vi.fn(),
  },
}))

const getApprovalRequest = vi.fn()

vi.mock('@/Requests/approvals', () => ({
  getApprovalRequest: (id: number) => getApprovalRequest(id),
}))

describe('pollApprovalOutcome', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    toastSuccess.mockClear()
    toastError.mockClear()
    getApprovalRequest.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('toasts the SAP doc number once the background job posts it', async () => {
    getApprovalRequest
      .mockResolvedValueOnce({ id: 1, overallStatus: 'Approved' })
      .mockResolvedValueOnce({ id: 1, overallStatus: 'Approved', sapResponseDocNum: '25000501' })

    pollApprovalOutcome(1, 'Purchase Order')

    await vi.advanceTimersByTimeAsync(3000)
    expect(toastSuccess).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(3000)
    expect(toastSuccess).toHaveBeenCalledWith('Purchase Order posted to SAP. Doc No: 25000501')
    expect(getApprovalRequest).toHaveBeenCalledTimes(2)
  })

  it('falls back to sapResponseDocEntry when no doc number is set', async () => {
    getApprovalRequest.mockResolvedValueOnce({ id: 1, overallStatus: 'Approved', sapResponseDocEntry: '501' })

    pollApprovalOutcome(1, 'Purchase Order')
    await vi.advanceTimersByTimeAsync(3000)

    expect(toastSuccess).toHaveBeenCalledWith('Purchase Order posted to SAP. Doc No: 501')
  })

  it('toasts an error and stops polling when the background job records a failure', async () => {
    getApprovalRequest.mockResolvedValueOnce({ id: 1, overallStatus: 'Failed', failureReason: 'SAP session expired' })

    pollApprovalOutcome(1, 'Payment')
    await vi.advanceTimersByTimeAsync(3000)

    expect(toastError).toHaveBeenCalledWith('Payment SAP posting failed: SAP session expired')

    await vi.advanceTimersByTimeAsync(60000)
    expect(getApprovalRequest).toHaveBeenCalledTimes(1)
  })

  it('gives up after the attempt budget without a doc number or failure', async () => {
    getApprovalRequest.mockResolvedValue({ id: 1, overallStatus: 'Approved' })

    pollApprovalOutcome(1, 'Purchase Order')
    await vi.advanceTimersByTimeAsync(3000 * 25)

    expect(getApprovalRequest).toHaveBeenCalledTimes(20)
    expect(toastSuccess).not.toHaveBeenCalled()
    expect(toastError).not.toHaveBeenCalled()
  })

  it('keeps polling through a transient fetch error', async () => {
    getApprovalRequest
      .mockRejectedValueOnce(new Error('network blip'))
      .mockResolvedValueOnce({ id: 1, overallStatus: 'Approved', sapResponseDocNum: '999' })

    pollApprovalOutcome(1, 'Purchase Order')
    await vi.advanceTimersByTimeAsync(3000)
    await vi.advanceTimersByTimeAsync(3000)

    expect(toastSuccess).toHaveBeenCalledWith('Purchase Order posted to SAP. Doc No: 999')
  })
})
