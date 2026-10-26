import { describe, expect, it } from 'vitest'
import InlineConfirm from '@/components/InlineConfirm.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'

async function render(props: Record<string, unknown> = {}) {
  return renderComponent(InlineConfirm, {
    props: { message: 'Remove this member?', confirmLabel: 'Remove', ...props },
    attachTo: document.body,
  })
}

describe('InlineConfirm', () => {
  it('moves focus into the block when it appears', async () => {
    const { wrapper } = await render()
    expect(wrapper.element.contains(document.activeElement)).toBe(true)
  })

  it('shows the message, the detail and the labels', async () => {
    const { wrapper } = await render({ detail: 'This cannot be undone.' })
    expect(wrapper.text()).toContain('Remove this member?')
    expect(wrapper.text()).toContain('This cannot be undone.')
    expect(wrapper.get('[data-testid="inline-confirm-confirm"]').text()).toBe('Remove')
    expect(wrapper.get('[data-testid="inline-confirm-cancel"]').text()).toBe(
      i18n.global.t('common.cancel'),
    )
  })

  it('emits cancel on Escape and on the cancel button', async () => {
    const { wrapper } = await render()
    await wrapper.trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('cancel')).toHaveLength(1)

    await wrapper.get('[data-testid="inline-confirm-cancel"]').trigger('click')
    expect(wrapper.emitted('cancel')).toHaveLength(2)
  })

  it('emits confirm from the confirm button', async () => {
    const { wrapper } = await render()
    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    expect(wrapper.emitted('confirm')).toHaveLength(1)
  })

  it('disables both buttons and ignores clicks while busy', async () => {
    const { wrapper } = await render({ busy: true })
    const confirm = wrapper.get('[data-testid="inline-confirm-confirm"]')
    const cancel = wrapper.get('[data-testid="inline-confirm-cancel"]')
    expect(confirm.attributes('disabled')).toBeDefined()
    expect(cancel.attributes('disabled')).toBeDefined()

    await confirm.trigger('click')
    await cancel.trigger('click')
    expect(wrapper.emitted('confirm')).toBeUndefined()
    expect(wrapper.emitted('cancel')).toBeUndefined()
  })

  it('renders only the message and the cancel button when showConfirm is false', async () => {
    const { wrapper } = await render({ showConfirm: false })
    expect(wrapper.find('[data-testid="inline-confirm-confirm"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="inline-confirm-cancel"]').exists()).toBe(true)
  })

  it('uses the localized confirm text when no label is given', async () => {
    const { wrapper } = await render({ confirmLabel: undefined })
    expect(wrapper.get('[data-testid="inline-confirm-confirm"]').text()).toBe(
      i18n.global.t('common.confirm'),
    )
  })
})
