import { describe, expect, it } from 'vitest'
import { pickHsnFromChapterId } from './hsnResolve'

describe('pickHsnFromChapterId', () => {
  const rows = [
    { AbsEntry: 19, ChapterID: '72.16.32', DisplayLabel: '72.16.32 - Other' },
    { AbsEntry: 24, ChapterID: '73.07.99', DisplayLabel: '73.07.99 - Elbows' },
  ]

  it('matches AbsEntry when item ChapterID is numeric', () => {
    expect(pickHsnFromChapterId('24', rows)).toEqual({
      HSNEntry: 24,
      HsnLabel: '73.07.99 - Elbows',
    })
  })

  it('matches tariff ChapterID string', () => {
    expect(pickHsnFromChapterId('73.07.99', rows)).toEqual({
      HSNEntry: 24,
      HsnLabel: '73.07.99 - Elbows',
    })
  })

  it('falls back to AbsEntry when master list empty', () => {
    expect(pickHsnFromChapterId('24', [])).toEqual({
      HSNEntry: 24,
      HsnLabel: '24',
    })
  })
})
