import React from 'react'
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SapDateInput } from './SapDateInput'

describe('SapDateInput', () => {
  it('still supports typing dd/MM/yyyy directly', async () => {
    const onChangeIso = vi.fn()
    render(<SapDateInput value="" onChangeIso={onChangeIso} label="Posting Date" />)

    const input = screen.getByLabelText('Posting Date')
    await userEvent.type(input, '05/03/2026')
    await userEvent.tab()

    expect(onChangeIso).toHaveBeenCalledWith('2026-03-05')
  })

  it('opens a calendar popover from the trailing icon and picks a date', async () => {
    const onChangeIso = vi.fn()
    render(<SapDateInput value="2026-03-15" onChangeIso={onChangeIso} label="Posting Date" />)

    await userEvent.click(screen.getByRole('button', { name: 'Open calendar' }))

    expect(screen.getByText('March 2026')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: '20' }))

    expect(onChangeIso).toHaveBeenCalledWith('2026-03-20')
  })

  it('"Today" and "Clear" buttons in the calendar work', async () => {
    const onChangeIso = vi.fn()
    render(<SapDateInput value="2026-03-15" onChangeIso={onChangeIso} label="Posting Date" />)

    await userEvent.click(screen.getByRole('button', { name: 'Open calendar' }))
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(onChangeIso).toHaveBeenLastCalledWith('')

    await userEvent.click(screen.getByRole('button', { name: 'Open calendar' }))
    await userEvent.click(screen.getByRole('button', { name: 'Today' }))
    expect(onChangeIso).toHaveBeenLastCalledWith(expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/))
  })

  it('does not open the calendar when disabled', async () => {
    const onChangeIso = vi.fn()
    render(<SapDateInput value="" onChangeIso={onChangeIso} label="Posting Date" disabled />)

    await userEvent.click(screen.getByRole('button', { name: 'Open calendar' }))
    expect(screen.queryByRole('button', { name: 'Clear' })).not.toBeInTheDocument()
  })
})
