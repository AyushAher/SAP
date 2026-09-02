import { beforeEach, describe, expect, it } from 'vitest'
import {
  clearCreateDraft,
  loadCreateDraft,
  newDraftSubassemblyKey,
  removeDraftSubassembly,
  saveCreateDraftHeader,
  upsertDraftSubassembly,
} from './productionOrderCreateDraft'

describe('productionOrderCreateDraft', () => {
  beforeEach(() => {
    clearCreateDraft()
  })

  it('keeps header edits and sub-assemblies in the same draft', () => {
    saveCreateDraftHeader({ ItemNumber: 'FG-001', PlannedQuantity: 1 })
    upsertDraftSubassembly({
      DraftKey: 'draft-1',
      ParentProductionOrderNo: '0/1',
      DrawingNo: 'DWG-1',
      ProductionOrderLines: [{ ItemNo: 'RM-100', PlannedQuantity: 2 }],
    })

    const draft = loadCreateDraft()
    expect(draft?.header.ItemNumber).toBe('FG-001')
    expect(draft?.subassemblies).toHaveLength(1)
    expect(draft?.subassemblies[0].DrawingNo).toBe('DWG-1')

    upsertDraftSubassembly({
      DraftKey: 'draft-1',
      ParentProductionOrderNo: '0/1',
      DrawingNo: 'DWG-1-edited',
      ProductionOrderLines: [{ ItemNo: 'RM-100', PlannedQuantity: 3 }],
    })
    expect(loadCreateDraft()?.subassemblies).toHaveLength(1)
    expect(loadCreateDraft()?.subassemblies[0].DrawingNo).toBe('DWG-1-edited')
    expect(loadCreateDraft()?.subassemblies[0].ProductionOrderLines?.[0].PlannedQuantity).toBe(3)
  })

  it('removes a drafted sub-assembly without touching the header', () => {
    saveCreateDraftHeader({ ItemNumber: 'FG-001' })
    upsertDraftSubassembly({ DraftKey: 'draft-1', ParentProductionOrderNo: '0/1' })
    upsertDraftSubassembly({ DraftKey: 'draft-2', ParentProductionOrderNo: '0/2' })
    removeDraftSubassembly('draft-1')

    const draft = loadCreateDraft()
    expect(draft?.header.ItemNumber).toBe('FG-001')
    expect(draft?.subassemblies.map((row) => row.DraftKey)).toEqual(['draft-2'])
  })

  it('allocates the next draft key', () => {
    expect(newDraftSubassemblyKey([])).toBe('draft-1')
    expect(newDraftSubassemblyKey([{ DraftKey: 'draft-2' }])).toBe('draft-3')
  })
})
