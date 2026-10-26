import { describe, expect, it } from 'vitest'
import StatusLabel from '@/components/StatusLabel.vue'
import { renderComponent } from '../helpers/render'

describe('StatusLabel', () => {
  it.each(['neutral', 'info', 'success', 'warning', 'danger'] as const)(
    'renders the slot text as a full-radius chip for %s',
    async (tone) => {
      const { wrapper } = await renderComponent(StatusLabel, {
        props: { tone },
        slots: { default: 'Active' },
      })
      expect(wrapper.text()).toBe('Active')
      expect(wrapper.classes()).toContain('rounded-full')
      expect(wrapper.attributes('data-tone')).toBe(tone)
    },
  )
})
