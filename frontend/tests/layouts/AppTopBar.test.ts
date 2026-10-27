import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AppTopBar from '@/layouts/AppTopBar.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'
import { stubViewport } from '../helpers/viewport'

const t = i18n.global.t

async function render(width: number) {
  stubViewport(width)
  const result = await renderComponent(AppTopBar, {
    slots: { organization: '<span data-testid="org-slot">Acme</span>' },
    global: { stubs: { teleport: true, NotificationBell: true, UserMenu: true } },
  })
  await flushPromises()
  return result
}

describe('AppTopBar', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows the Go to button with its shortcut at 1280 px and an icon button below', async () => {
    const wide = await render(1280)
    const button = wide.wrapper.get('[data-testid="app-header-palette"]')
    expect(button.text()).toContain(t('layout.palette.trigger'))
    expect(button.find('kbd').exists()).toBe(true)
    expect(wide.wrapper.find('[data-testid="org-slot"]').exists()).toBe(true)
    wide.wrapper.unmount()

    const narrow = await render(834)
    const icon = narrow.wrapper.get('[data-testid="app-header-palette"]')
    expect(icon.text()).toBe('')
    expect(icon.find('kbd').exists()).toBe(false)
    expect(icon.attributes('aria-label')).toBe(t('layout.palette.trigger'))

    await icon.trigger('click')
    expect(storeIn(narrow.pinia, useLayoutStore).paletteOpen).toBe(true)
  })

  it('gives every icon button an accessible name and a 44 px target below 1200 px', async () => {
    const { wrapper } = await render(390)

    const buttons = wrapper.findAll('button')
    expect(buttons.length).toBeGreaterThanOrEqual(2)
    for (const button of buttons) {
      expect(button.attributes('aria-label')).toBeTruthy()
      expect(button.classes()).toEqual(
        expect.arrayContaining(['max-desktop:min-h-11', 'max-desktop:min-w-11']),
      )
    }
  })

  it('offers the menu button only on a phone', async () => {
    const phone = await render(390)
    expect(phone.wrapper.find(`button[aria-label="${t('nav.openMenu')}"]`).exists()).toBe(true)
    phone.wrapper.unmount()

    const tablet = await render(834)
    expect(tablet.wrapper.find(`button[aria-label="${t('nav.openMenu')}"]`).exists()).toBe(false)
  })
})
