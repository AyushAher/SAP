import React from 'react'
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { BlockingLoader } from './BlockingLoader'

describe('BlockingLoader', () => {
  it('renders nothing when not visible', () => {
    const { container } = render(<BlockingLoader visible={false} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders a blocking overlay with label when visible', () => {
    render(<BlockingLoader visible label="Saving changes…" />)
    expect(screen.getByRole('status', { name: 'Saving changes…' })).toBeInTheDocument()
    expect(screen.getByText('Saving changes…')).toBeInTheDocument()
  })
})
