import { describe, expect, it } from 'vitest'
import InlineNotice from '@/components/InlineNotice.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'

describe('InlineNotice', () => {
  it('uses role alert for danger and role status otherwise', async () => {
    const danger = await renderComponent(InlineNotice, {
      props: { tone: 'danger' },
      slots: { default: 'Boom' },
    })
    expect(danger.wrapper.attributes('role')).toBe('alert')

    for (const tone of ['info', 'success', 'warning'] as const) {
      const { wrapper } = await renderComponent(InlineNotice, { props: { tone } })
      expect(wrapper.attributes('role')).toBe('status')
    }
  })

  it('renders the default and action slots', async () => {
    const { wrapper } = await renderComponent(InlineNotice, {
      props: { tone: 'info' },
      slots: { default: 'Heads up', action: '<button>Retry</button>' },
    })
    expect(wrapper.text()).toContain('Heads up')
    expect(wrapper.find('button').text()).toBe('Retry')
  })

  it('shows a close button only when dismissible and emits dismiss', async () => {
    const plain = await renderComponent(InlineNotice, { props: { tone: 'info' } })
    expect(plain.wrapper.find('[data-testid="inline-notice-dismiss"]').exists()).toBe(false)

    const { wrapper } = await renderComponent(InlineNotice, {
      props: { tone: 'info', dismissible: true },
    })
    const close = wrapper.get('[data-testid="inline-notice-dismiss"]')
    expect(close.attributes('aria-label')).toBe(i18n.global.t('common.close'))
    await close.trigger('click')
    expect(wrapper.emitted('dismiss')).toHaveLength(1)
  })
})
