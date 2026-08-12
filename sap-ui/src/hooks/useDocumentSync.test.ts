import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useDocumentSync } from '@/hooks/useDocumentSync'

const toastSuccess = vi.fn()
const toastError = vi.fn()

vi.mock('@/helpers/toast', () => ({
  toast: {
    success: (message: string) => toastSuccess(message),
    error: (message: string) => toastError(message),
  },
}))

describe('useDocumentSync', () => {
  beforeEach(() => {
    toastSuccess.mockClear()
    toastError.mockClear()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('bumps the table key after a row sync so the list refetches', async () => {
    const syncRow = vi.fn(async () => ({ message: 'Updated production order 900101 from SAP.' }))

    const { result } = renderHook(() => useDocumentSync({ syncRow }))
    const initialKey = result.current.tableKey

    await act(async () => {
      await result.current.handleSyncRow(646)
    })

    expect(syncRow).toHaveBeenCalledWith(646)
    expect(result.current.tableKey).toBe(initialKey + 1)
    expect(result.current.syncingKey).toBeNull()
    expect(toastSuccess).toHaveBeenCalledWith('Updated production order 900101 from SAP.')
  })

  it('surfaces a row sync failure without refetching the list', async () => {
    const syncRow = vi.fn(async () => {
      throw new Error('SAP is unavailable.')
    })

    const { result } = renderHook(() => useDocumentSync({ syncRow }))
    const initialKey = result.current.tableKey

    await act(async () => {
      await result.current.handleSyncRow(646)
    })

    expect(result.current.syncError).toBe('SAP is unavailable.')
    expect(result.current.tableKey).toBe(initialKey)
    expect(toastError).toHaveBeenCalledWith('SAP is unavailable.')
  })

  it('keeps the row sync callback stable so pickers do not refetch in a loop', () => {
    const syncRow = vi.fn(async () => ({ message: 'ok' }))

    const { result, rerender } = renderHook(() => useDocumentSync({ syncRow }))
    const first = result.current.handleSyncRow
    rerender()

    expect(result.current.handleSyncRow).toBe(first)
  })

  it('shows progress while a queued full sync is running and refetches once it succeeds', async () => {
    const enqueueFullSync = vi.fn(async () => ({ message: 'Full sync job queued.' }))
    const getSyncStatus = vi
      .fn()
      .mockResolvedValueOnce({ status: 'Idle' })
      .mockResolvedValueOnce({ status: 'Running', message: 'Running batch 1…' })
      .mockResolvedValue({ status: 'Succeeded', message: 'Sync completed: 16 production order(s).' })

    const { result } = renderHook(() =>
      useDocumentSync({ enqueueFullSync, getSyncStatus, syncRow: vi.fn(async () => ({ message: 'ok' })) }),
    )
    const initialKey = result.current.tableKey

    await act(async () => {
      await result.current.handleSyncAll()
    })

    expect(enqueueFullSync).toHaveBeenCalledTimes(1)
    expect(result.current.syncingAll).toBe(true)
    expect(result.current.syncProgress).toBe('Running batch 1…')

    await waitFor(() => expect(result.current.syncingAll).toBe(false), { timeout: 5000 })
    expect(result.current.tableKey).toBe(initialKey + 1)
    expect(toastSuccess).toHaveBeenCalledWith('Sync completed: 16 production order(s).')
  })

  it('reports a failed full sync instead of leaving the spinner running', async () => {
    const enqueueFullSync = vi.fn(async () => ({ message: 'queued' }))
    const getSyncStatus = vi
      .fn()
      .mockResolvedValueOnce({ status: 'Idle' })
      .mockResolvedValue({ status: 'Failed', message: 'Full sync failed: SAP session expired.' })

    const { result } = renderHook(() =>
      useDocumentSync({ enqueueFullSync, getSyncStatus, syncRow: vi.fn(async () => ({ message: 'ok' })) }),
    )

    await act(async () => {
      await result.current.handleSyncAll()
    })

    await waitFor(() => expect(result.current.syncingAll).toBe(false), { timeout: 5000 })
    expect(result.current.syncError).toBe('Full sync failed: SAP session expired.')
    expect(toastError).toHaveBeenCalledWith('Full sync failed: SAP session expired.')
  })
})
