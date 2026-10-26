import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { RouterView } from 'vue-router'
import { defineComponent, onMounted, onUnmounted } from 'vue'
import AccessProblemState from '@/components/AccessProblemState.vue'
import AppLayout from '@/layouts/AppLayout.vue'
import AppRail from '@/layouts/AppRail.vue'
import router from '@/router'
import i18n from '@/plugins/i18n'
import { apiClient } from '@/api/client'
import { accessProblemOf } from '@/lib/access-problem'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'
import { stubViewport } from '../helpers/viewport'
import { stubView } from '../helpers/router-view-stubs'

// The shell tests run on the real router; stub the lazy views they never look at. AppLayout and
// NotFoundView stay real: the shell is under test, and so is its not-found state.
vi.mock('@/views/HomeView.vue', () => stubView())
vi.mock('@/views/OrganizationsView.vue', () => stubView())
vi.mock('@/views/LoginView.vue', () => stubView())

const t = i18n.global.t

const profileUnmounted = vi.fn()
const ProfilePanelStub = defineComponent({
  props: { visible: Boolean },
  setup() {
    onUnmounted(profileUnmounted)
  },
  template: '<div data-testid="profile-stub" :data-visible="visible" />',
})

// The real settings panel, palette and profile panel have their own tests; the palette listens on `window`.
const overlayStubs = {
  AppearanceSettings: { template: '<div data-testid="settings-stub" />' },
  CommandPalette: { template: '<div data-testid="palette-stub" />' },
  ProfilePanel: ProfilePanelStub,
}

const shellStubs = { ...overlayStubs, AppRail: true, AppTopBar: true, AppFooter: true }

describe('AppLayout', () => {
  // Cleared before, not after: the previous test's wrapper is unmounted by tests/setup.ts after this
  // file's own afterEach hooks have run.
  beforeEach(() => {
    profileUnmounted.mockClear()
    stubViewport(1280)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('renders the rail, top bar, the routed content and the footer in that order', async () => {
    const { wrapper } = await renderComponent(AppLayout, {
      global: {
        stubs: {
          ...overlayStubs,
          AppRail: { template: '<div class="marker-rail" />' },
          AppTopBar: { template: '<div class="marker-top-bar" />' },
          AppFooter: { template: '<div class="marker-footer" />' },
        },
      },
    })

    const html = wrapper.html()
    expect(wrapper.find('[data-testid="app-shell"]').exists()).toBe(true)
    expect(wrapper.find('[data-ambient]').exists()).toBe(true)
    expect(html.indexOf('marker-rail')).toBeLessThan(html.indexOf('marker-top-bar'))
    expect(html.indexOf('marker-top-bar')).toBeLessThan(html.indexOf('<main'))
    expect(html.indexOf('<main')).toBeLessThan(html.indexOf('marker-footer'))
  })

  it('renders the rail layout whatever menu mode is left in localStorage', async () => {
    localStorage.setItem('layout-menu-mode', 'horizontal')
    localStorage.setItem('layout-menu-theme', 'transparent')
    localStorage.setItem('sidebar-mode', 'full')

    const { wrapper } = await renderComponent(AppLayout, {
      global: { stubs: { ...overlayStubs, AppTopBar: true, AppFooter: true } },
    })

    expect(wrapper.findComponent(AppRail).exists()).toBe(true)
    expect(wrapper.find('[data-testid="app-sidebar"]').exists()).toBe(true)
    expect(wrapper.find('[data-mode]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-top-menu"]').exists()).toBe(false)
  })

  it('neither reads nor writes the old layout keys', async () => {
    const reads = vi.spyOn(Storage.prototype, 'getItem')
    const writes = vi.spyOn(Storage.prototype, 'setItem')

    const { pinia } = await renderComponent(AppLayout, { global: { stubs: shellStubs } })
    const layout = storeIn(pinia, useLayoutStore)
    layout.openMobile()
    layout.openSettings()

    const touched = [...reads.mock.calls, ...writes.mock.calls].map(([key]) => key)
    for (const key of [
      'layout-menu-mode',
      'layout-menu-theme',
      'layout-drawer-pinned',
      'layout-static-inactive',
      'sidebar-mode',
    ]) {
      expect(touched).not.toContain(key)
    }
  })

  it('mounts the settings panel, the palette and the profile panel once', async () => {
    const { wrapper, pinia } = await renderComponent(AppLayout, {
      global: { stubs: shellStubs },
    })

    expect(wrapper.findAll('[data-testid="settings-stub"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="palette-stub"]')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="profile-stub"]')).toHaveLength(1)

    storeIn(pinia, useLayoutStore).openProfile()
    await flushPromises()
    expect(wrapper.get('[data-testid="profile-stub"]').attributes('data-visible')).toBe('true')
    expect(profileUnmounted).not.toHaveBeenCalled()
  })

  it('scrolls main back to the top on a path change and keeps it on a query change', async () => {
    const { wrapper, router } = await renderComponent(AppLayout, {
      global: { stubs: shellStubs },
    })
    const main = wrapper.find('main').element as HTMLElement
    const scrollTo = vi.fn()
    main.scrollTo = scrollTo as unknown as HTMLElement['scrollTo']

    await router.push('/?page=2')
    await flushPromises()
    expect(scrollTo).not.toHaveBeenCalled()

    await router.push('/organizations')
    await flushPromises()
    expect(scrollTo).toHaveBeenCalledWith({ top: 0 })
  })

  it('starts the notification polling and realtime connection, and stops them on unmount', async () => {
    const { wrapper, pinia } = await renderComponent(AppLayout, {
      global: { stubs: shellStubs },
    })
    const notifications = storeIn(pinia, useNotificationsStore)

    expect(notifications.startPolling).toHaveBeenCalledTimes(1)
    expect(notifications.connectRealtime).toHaveBeenCalledTimes(1)

    wrapper.unmount()

    expect(notifications.stopPolling).toHaveBeenCalledTimes(1)
    expect(notifications.disconnectRealtime).toHaveBeenCalledTimes(1)
  })

  describe('access problems', () => {
    const pageMounted = vi.fn()
    const GatedPage = defineComponent({
      setup() {
        onMounted(pageMounted)
      },
      template: '<p data-testid="gated-page">Gated page</p>',
    })
    const LeakyPage = defineComponent({
      components: { AccessProblemState },
      data: () => ({ kind: null as 'not-found' | 'forbidden' | null }),
      async mounted() {
        try {
          await apiClient.get('/api/leaky')
        } catch (error) {
          this.kind = accessProblemOf(error)
        }
      },
      template: '<AccessProblemState v-if="kind" :kind="kind" />',
    })

    let removeTestRoutes: () => void = () => {}

    // The real router, plus a test-only shell whose children carry the metas under test.
    async function renderShell(path: string, auth: Record<string, unknown> = {}) {
      const pinia = createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,
        initialState: {
          auth: { account: { id: 'a-1' }, organizationId: 'org-1', permissions: [], ...auth },
        },
      })
      removeTestRoutes = router.addRoute({
        path: '/test',
        component: AppLayout,
        children: [
          {
            path: 'reports',
            component: GatedPage,
            meta: { requiresAuth: true, permission: 'reports.view' },
          },
          {
            path: 'admin',
            component: GatedPage,
            meta: { requiresAuth: true, systemAdministrator: true },
          },
          { path: 'leaky', component: LeakyPage, meta: { requiresAuth: true } },
        ],
      })
      await router.replace('/')
      await router.push(path)
      await router.isReady()

      const wrapper = mount(RouterView, {
        global: { plugins: [pinia, router], stubs: shellStubs },
      })
      await flushPromises()

      return { wrapper, router }
    }

    beforeEach(() => pageMounted.mockClear())

    afterEach(() => removeTestRoutes())

    it('shows the forbidden state in the shell for a page whose permission the user lacks', async () => {
      const { wrapper, router } = await renderShell('/test/reports')

      expect(wrapper.find('[data-testid="app-shell"]').exists()).toBe(true)
      expect(wrapper.get('h1').text()).toBe(t('accessProblem.forbiddenTitle'))
      expect(router.currentRoute.value.fullPath).toBe('/test/reports')
    })

    it('does not mount the routed page behind the forbidden state', async () => {
      const { wrapper } = await renderShell('/test/reports')

      expect(wrapper.find('[data-testid="gated-page"]').exists()).toBe(false)
      expect(pageMounted).not.toHaveBeenCalled()
    })

    it('shows the forbidden state, not the page, to a user who is not a system administrator', async () => {
      const { wrapper } = await renderShell('/test/admin', { isSystemAdministrator: false })

      expect(wrapper.get('h1').text()).toBe(t('accessProblem.forbiddenTitle'))
      expect(wrapper.find('[data-testid="gated-page"]').exists()).toBe(false)
      expect(pageMounted).not.toHaveBeenCalled()
    })

    it('renders the routed page when the user holds the permission', async () => {
      const { wrapper } = await renderShell('/test/reports', { permissions: ['reports.view'] })

      expect(wrapper.find('[data-testid="gated-page"]').exists()).toBe(true)
      expect(wrapper.find('h1').exists()).toBe(false)
    })

    it('shows the not-found state inside the shell for an unknown path', async () => {
      const { wrapper, router } = await renderShell('/no/such/page')

      expect(wrapper.find('[data-testid="app-shell"]').exists()).toBe(true)
      expect(wrapper.get('main h1').text()).toBe(t('accessProblem.notFoundTitle'))
      expect(router.currentRoute.value.fullPath).toBe('/no/such/page')
    })

    it('never renders the problem detail, title or extensions of a 404', async () => {
      server.use(
        http.get('*/api/leaky', () =>
          HttpResponse.json(
            {
              status: 404,
              title: 'Sao Mai Tech',
              detail: 'Role Trưởng IT',
              code: 'X',
              organizationName: 'Sao Mai Tech',
            },
            { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
          ),
        ),
      )
      const { wrapper } = await renderShell('/test/leaky')
      await vi.waitFor(() => expect(wrapper.find('main h1').exists()).toBe(true))

      const main = wrapper.get('main')
      expect(main.text()).toBe(
        [
          t('accessProblem.notFoundTitle'),
          t('accessProblem.notFoundText'),
          t('accessProblem.goHome'),
        ].join(''),
      )
      for (const html of [wrapper.html(), document.body.innerHTML]) {
        expect(html).not.toContain('Sao Mai Tech')
        expect(html).not.toContain('Trưởng IT')
      }
    })
  })
})
