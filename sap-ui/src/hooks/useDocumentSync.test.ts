import { act, renderHook } from '@testing-library/react'
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

  it('queues a full sync and then leaves the list alone', async () => {
    const enqueueFullSync = vi.fn(async () => ({ message: 'Full sync job queued.' }))
    const getSyncStatus = vi.fn(async () => ({ status: 'Running', message: 'Running batch 1…' }))

    const { result } = renderHook(() =>
      useDocumentSync({ enqueueFullSync, getSyncStatus, syncRow: vi.fn(async () => ({ message: 'ok' })) }),
    )
    const initialKey = result.current.tableKey

    await act(async () => {
      await result.current.handleSyncAll()
    })

    expect(enqueueFullSync).toHaveBeenCalledTimes(1)
    expect(result.current.syncMessage).toBe('Full sync job queued.')
    expect(result.current.startingSync).toBe(false)
    expect(toastSuccess).toHaveBeenCalledWith('Full sync job queued.')
    expect(result.current.tableKey).toBe(initialKey)
    expect(getSyncStatus).not.toHaveBeenCalled()
  })

  it('never reads the sync status on its own, before or after queueing', async () => {
    vi.useFakeTimers()
    const getSyncStatus = vi.fn(async () => ({ status: 'Running', message: 'Running batch 1…' }))

    const { result } = renderHook(() =>
      useDocumentSync({
        enqueueFullSync: vi.fn(async () => ({ message: 'queued' })),
        getSyncStatus,
        syncRow: vi.fn(async () => ({ message: 'ok' })),
      }),
    )

    await act(async () => {
      await result.current.handleSyncAll()
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000)
    })

    expect(getSyncStatus).not.toHaveBeenCalled()
    expect(result.current.tableKey).toBe(0)
  })

  it('reads the status once and reloads the rows when the user refreshes', async () => {
    const getSyncStatus = vi.fn(async () => ({
      status: 'Succeeded',
      message: 'Sync completed: 16 production order(s).',
    }))

    const { result } = renderHook(() =>
      useDocumentSync({
        enqueueFullSync: vi.fn(async () => ({ message: 'queued' })),
        getSyncStatus,
        syncRow: vi.fn(async () => ({ message: 'ok' })),
      }),
    )
    const initialKey = result.current.tableKey

    await act(async () => {
      await result.current.handleRefresh()
    })

    expect(getSyncStatus).toHaveBeenCalledTimes(1)
    expect(result.current.syncMessage).toBe('Sync completed: 16 production order(s).')
    expect(result.current.tableKey).toBe(initialKey + 1)
    expect(result.current.refreshing).toBe(false)
  })

  it('shows a failed sync when the user refreshes, and still reloads the rows', async () => {
    const getSyncStatus = vi.fn(async () => ({
      status: 'Failed',
      message: 'Full sync failed: SAP session expired.',
    }))

    const { result } = renderHook(() =>
      useDocumentSync({
        enqueueFullSync: vi.fn(async () => ({ message: 'queued' })),
        getSyncStatus,
        syncRow: vi.fn(async () => ({ message: 'ok' })),
      }),
    )

    await act(async () => {
      await result.current.handleRefresh()
    })

    expect(result.current.syncError).toBe('Full sync failed: SAP session expired.')
    expect(result.current.syncMessage).toBeNull()
    expect(result.current.tableKey).toBe(1)
  })

  it('reports a full sync that could not be queued', async () => {
    const enqueueFullSync = vi.fn(async () => {
      throw new Error('Sync already running.')
    })

    const { result } = renderHook(() =>
      useDocumentSync({
        enqueueFullSync,
        getSyncStatus: vi.fn(async () => null),
        syncRow: vi.fn(async () => ({ message: 'ok' })),
      }),
    )

    await act(async () => {
      await result.current.handleSyncAll()
    })

    expect(result.current.syncError).toBe('Sync already running.')
    expect(result.current.startingSync).toBe(false)
    expect(toastError).toHaveBeenCalledWith('Sync already running.')
  })
})
