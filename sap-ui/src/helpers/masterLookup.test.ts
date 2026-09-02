import { describe, expect, it } from 'vitest'
import { formatCodeWithName, nameFromCodeWithNameLabel, nextAutoFilledName } from './masterLookup'

describe('nameFromCodeWithNameLabel', () => {
  it('returns the name after the code prefix', () => {
    expect(nameFromCodeWithNameLabel('_SYS00000000893 - Raw material', '_SYS00000000893'))
      .toBe('Raw material')
    expect(nameFromCodeWithNameLabel('RM1 - BEAM 250 MM - GRADE A', 'RM1'))
      .toBe('BEAM 250 MM - GRADE A')
  })

  it('returns undefined when the label is only the code', () => {
    expect(nameFromCodeWithNameLabel('RM1', 'RM1')).toBeUndefined()
    expect(nameFromCodeWithNameLabel('')).toBeUndefined()
  })
})

describe('nextAutoFilledName', () => {
  it('fills an empty description from the master name', () => {
    expect(nextAutoFilledName('', undefined, 'Raw material')).toBe('Raw material')
    expect(nextAutoFilledName(undefined, undefined, 'BEAM 250 MM')).toBe('BEAM 250 MM')
  })

  it('replaces the previous auto-fill when the master changes', () => {
    expect(nextAutoFilledName('Raw material', 'Raw material', 'Freight')).toBe('Freight')
  })

  it('keeps a user-typed description', () => {
    expect(nextAutoFilledName('Custom freight note', 'Raw material', 'Freight'))
      .toBe('Custom freight note')
  })
})

describe('formatCodeWithName', () => {
  it('joins code and name', () => {
    expect(formatCodeWithName('RM1', 'BEAM 250 MM')).toBe('RM1 - BEAM 250 MM')
  })
})
