import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { Building, Home } from '@vicons/tabler'
import AppRail from '@/layouts/AppRail.vue'
import AppTopBar from '@/layouts/AppTopBar.vue'
import i18n from '@/plugins/i18n'
import { storeIn } from '../helpers/pinia'
import { stubViewport } from '../helpers/viewport'

const t = i18n.global.t
const stub = { template: '<div />' }

async function mountRail(width: number, initialPath = '/') {
  const viewport = stubViewport(width)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        component: stub,
        children: [
          {
            path: '',
            name: 'home',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.home', icon: Home, order: 1 } },
          },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.organizations', icon: Building, group: 'organization' } },
          },
          {
            path: 'members',
            name: 'members',
            component: stub,
            meta: { activeMenu: 'organizations' },
          },
        ],
      },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })
  const Host = defineComponent({
    components: { AppRail, AppTopBar },
    template: '<AppTopBar /><AppRail />',
  })
  const wrapper = mount(Host, {
    global: {
      plugins: [pinia, router],
      stubs: { teleport: true, NotificationBell: true, UserMenu: true },
    },
  })
  await flushPromises()
  return { wrapper, router, viewport, layout: storeIn(pinia, useLayoutStore) }
}

describe('AppRail', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows the wide rail with group headings at 1280 px', async () => {
    const { wrapper } = await mountRail(1280)

    const rail = wrapper.get('[data-testid="app-sidebar"]')
    expect(rail.classes()).toContain('glass-thin')
    expect(rail.text()).toContain(t('app.name'))
    expect(rail.text()).toContain(t('nav.groups.organization'))
    expect(rail.text()).toContain(t('nav.home'))
    expect(rail.text()).toContain(t('nav.organizations'))
  })

  it('shows the icon rail with labelled icon links at 834 px', async () => {
    const { wrapper } = await mountRail(834)

    const rail = wrapper.get('[data-testid="app-sidebar"]')
    expect(rail.text()).not.toContain(t('nav.home'))
    expect(rail.text()).not.toContain(t('nav.groups.organization'))
    const links = rail.findAll('a[aria-label]')
    expect(links.map((link) => link.attributes('aria-label'))).toEqual(
      expect.arrayContaining([t('nav.home'), t('nav.organizations')]),
    )
    for (const link of links) {
      expect(link.attributes('title')).toBe(link.attributes('aria-label'))
    }
    expect(rail.findAll('[role="separator"]')).toHaveLength(1)
  })

  it('shows no rail at 390 px and opens the slide-in menu from the top bar', async () => {
    const { wrapper, layout } = await mountRail(390)
    expect(wrapper.find('[data-testid="app-sidebar"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-sidebar-mobile"]').exists()).toBe(false)

    await wrapper.get(`button[aria-label="${t('nav.openMenu')}"]`).trigger('click')
    await flushPromises()

    expect(layout.mobileOpen).toBe(true)
    const menu = wrapper.get('[data-testid="app-sidebar-mobile"]')
    expect(menu.text()).toContain(t('nav.home'))
    expect(menu.text()).toContain(t('layout.palette.trigger'))
  })

  it('opens the palette from the Go to box of the slide-in menu and closes the menu', async () => {
    const { wrapper, layout } = await mountRail(390)
    layout.openMobile()
    await flushPromises()

    await wrapper.get('[data-testid="app-sidebar-mobile-palette"]').trigger('click')

    expect(layout.paletteOpen).toBe(true)
    expect(layout.mobileOpen).toBe(false)
  })

  it('marks the current page with aria-current', async () => {
    const { wrapper } = await mountRail(1280, '/organizations')

    const rail = wrapper.get('[data-testid="app-sidebar"]')
    const current = rail.findAll('a[aria-current="page"]')
    expect(current).toHaveLength(1)
    expect(current[0]!.text()).toContain(t('nav.organizations'))
  })

  it('marks the item named by meta.activeMenu for a page without its own item', async () => {
    const { wrapper } = await mountRail(1280, '/members')

    const current = wrapper.get('[data-testid="app-sidebar"]').findAll('a[aria-current="page"]')
    expect(current).toHaveLength(1)
    expect(current[0]!.text()).toContain(t('nav.organizations'))
  })

  it('heads the groups of the slide-in menu with their names', async () => {
    const { wrapper, layout } = await mountRail(390)
    layout.openMobile()
    await flushPromises()

    const menu = wrapper.get('[data-testid="app-sidebar-mobile"]')
    expect(menu.text()).toContain(t('nav.groups.organization'))
    expect(menu.text()).toContain(t('nav.organizations'))
  })

  it('closes the slide-in menu when the screen grows past a phone', async () => {
    const { layout, viewport } = await mountRail(390)
    layout.openMobile()
    await flushPromises()
    expect(layout.mobileOpen).toBe(true)

    viewport.setWidth(834)
    await flushPromises()

    expect(layout.mobileOpen).toBe(false)
  })

  it('closes the slide-in menu after a navigation', async () => {
    const { router, layout } = await mountRail(390)
    layout.openMobile()
    await flushPromises()

    await router.push('/organizations')
    await flushPromises()

    expect(layout.mobileOpen).toBe(false)
  })
})
