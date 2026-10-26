import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AppSheet from '@/components/AppSheet.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'
import { stubViewport } from '../helpers/viewport'

async function openSheet(width: number) {
  stubViewport(width)
  const result = await renderComponent(AppSheet, {
    props: { show: true, title: 'Edit member' },
    slots: { default: '<p>Body</p>', footer: '<button>Save</button>' },
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

describe('AppSheet', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('opens on the right without a dimming mask at 1280 px', async () => {
    const { wrapper } = await openSheet(1280)
    const drawer = wrapper.get('[data-testid="app-sheet"]')
    expect(drawer.classes()).toContain('n-drawer--right-placement')
    expect(drawer.attributes('style')).toContain('440px')
    expect(wrapper.find('.n-drawer-mask').attributes('style') ?? '').not.toMatch(/background/)
  })

  it.each([834, 390])('opens from the bottom at full width at %i px', async (width) => {
    const { wrapper } = await openSheet(width)
    const drawer = wrapper.get('[data-testid="app-sheet"]')
    expect(drawer.classes()).toContain('n-drawer--bottom-placement')
    expect(drawer.attributes('style')).toContain('90dvh')
  })

  it('names the dialog with its title and renders the slots', async () => {
    const { wrapper } = await openSheet(1280)
    const drawer = wrapper.get('[data-testid="app-sheet"]')
    expect(drawer.attributes('role')).toBe('dialog')
    expect(drawer.attributes('aria-label')).toBe('Edit member')
    expect(wrapper.text()).toContain('Edit member')
    expect(wrapper.text()).toContain('Body')
    expect(wrapper.text()).toContain('Save')
  })

  it.each([1280, 390])('keeps focus inside the open sheet at %i px', async (width) => {
    stubViewport(width)
    const outside = document.createElement('button')
    document.body.appendChild(outside)
    const { wrapper } = await renderComponent(AppSheet, {
      props: { show: true, title: 'Edit member' },
      slots: { default: '<p>Body</p>', footer: '<button>Save</button>' },
      global: { stubs: { teleport: true } },
      attachTo: document.body,
    })
    await flushPromises()
    const container = wrapper.get('.n-drawer-container').element

    expect(container.contains(document.activeElement)).toBe(true)

    outside.focus()
    await flushPromises()
    expect(document.activeElement).not.toBe(outside)
    expect(container.contains(document.activeElement)).toBe(true)

    outside.remove()
  })

  it('closes from the close button', async () => {
    const { wrapper } = await openSheet(1280)
    const close = wrapper.get('[data-testid="app-sheet-close"]')
    expect(close.attributes('aria-label')).toBe(i18n.global.t('common.close'))
    await close.trigger('click')
    expect(wrapper.emitted('update:show')?.[0]).toEqual([false])
  })

  it('closes from Escape', async () => {
    const { wrapper } = await openSheet(1280)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true }))
    await flushPromises()
    expect(wrapper.emitted('update:show')?.[0]).toEqual([false])
  })
})
