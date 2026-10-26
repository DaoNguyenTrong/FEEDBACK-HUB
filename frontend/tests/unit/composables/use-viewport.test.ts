import { afterEach, describe, expect, it, vi } from 'vitest'
import { stubViewport } from '../../helpers/viewport'
import { useViewport } from '@/composables/use-viewport'
import { withSetup } from '../../helpers/with-setup'

describe('useViewport', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('reports phone below 768 px, desktop from 1200 px and tablet in between', async () => {
    const viewport = stubViewport(390)
    const { result } = await withSetup(() => useViewport())
    expect(result.isPhone.value).toBe(true)
    expect(result.isDesktop.value).toBe(false)

    viewport.setWidth(834)
    expect([result.isPhone.value, result.isDesktop.value]).toEqual([false, false])

    viewport.setWidth(1280)
    expect([result.isPhone.value, result.isDesktop.value]).toEqual([false, true])
  })
})
