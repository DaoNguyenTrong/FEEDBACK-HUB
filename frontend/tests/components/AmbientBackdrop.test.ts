import { describe, expect, it } from 'vitest'
import AmbientBackdrop from '@/components/AmbientBackdrop.vue'
import { renderComponent } from '../helpers/render'

describe('AmbientBackdrop', () => {
  it('draws three blobs at full strength and two at soft strength', async () => {
    const full = await renderComponent(AmbientBackdrop, { props: { strength: 'full' } })
    expect(full.wrapper.element.children).toHaveLength(3)
    expect(full.wrapper.attributes('style')).toContain('--ambient-full')

    const soft = await renderComponent(AmbientBackdrop, { props: { strength: 'soft' } })
    expect(soft.wrapper.element.children).toHaveLength(2)
    expect(soft.wrapper.attributes('style')).toContain('--ambient-soft')
  })

  it('is hidden from assistive technology and marked for the motion fallbacks', async () => {
    const { wrapper } = await renderComponent(AmbientBackdrop, { props: { strength: 'full' } })
    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.attributes('data-ambient')).toBeDefined()
  })
})
