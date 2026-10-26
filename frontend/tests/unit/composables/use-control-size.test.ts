import { afterEach, describe, expect, it, vi } from 'vitest'
import { useControlSize } from '@/composables/use-control-size'
import { stubViewport } from '../../helpers/viewport'
import { withSetup } from '../../helpers/with-setup'

describe('useControlSize', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('returns large below 768 px and medium from 768 px', async () => {
    const viewport = stubViewport(390)
    const { result } = await withSetup(() => useControlSize())
    expect(result.value).toBe('large')

    viewport.setWidth(767)
    expect(result.value).toBe('large')

    viewport.setWidth(768)
    expect(result.value).toBe('medium')
  })
})
