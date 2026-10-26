import type { RouteLocationNormalized } from 'vue-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import router from '@/router'
import { useHealthStore } from '@/stores/health'
import { setupTestPinia } from '../../helpers/pinia'
import { stubView } from '../../helpers/router-view-stubs'

// This file checks the guards, not the views: stub every lazy view it navigates to.
vi.mock('@/layouts/AppLayout.vue', () => stubView())
vi.mock('@/views/HomeView.vue', () => stubView())
vi.mock('@/views/OrganizationsView.vue', () => stubView())
vi.mock('@/views/LoginView.vue', () => stubView())
vi.mock('@/views/NotFoundView.vue', () => stubView())

// Exercises the real router's `beforeEach` wiring (not just the pure
// `resolveGuardRedirect`) — this is the seam that redirected an F5-with-BE-down
// visitor to /login.
describe('router guard (wired, real router)', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  afterEach(async () => {
    await router.replace('/login')
  })

  it('redirects an unauthenticated visitor on a requiresAuth route to /login, keeping the destination', async () => {
    await router.push('/organizations')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/organizations')
  })

  it('sends a signed-out visitor on an unknown path to login, keeping the path', async () => {
    await router.push('/no/such/page')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/no/such/page')
  })

  it('shows an unknown path to a signed-in user as not-found inside the layout', async () => {
    useAuthStore().account = { id: 'a-1' } as never

    await router.push('/no/such/page')

    const route = router.currentRoute.value
    expect(route.name).toBe('not-found')
    expect(route.fullPath).toBe('/no/such/page')
    expect(route.matched[0]?.path).toBe('/')
  })

  describe('gated routes', () => {
    const stub = { template: '<div />' }
    const removers: (() => void)[] = []

    beforeEach(() => {
      removers.push(
        router.addRoute({
          path: '/org-only',
          name: 'org-only',
          component: stub,
          meta: { requiresAuth: true, requiresOrganization: true },
        }),
        router.addRoute({
          path: '/reports-only',
          name: 'reports-only',
          component: stub,
          meta: { requiresAuth: true, permission: 'reports.view' },
        }),
      )
    })

    afterEach(() => {
      removers.splice(0).forEach((remove) => remove())
    })

    it('sends a personal-context user on an organization route to home', async () => {
      useAuthStore().account = { id: 'a-1' } as never

      await router.push('/org-only')

      expect(router.currentRoute.value.fullPath).toBe(router.resolve(useHomeRoute()).fullPath)
    })

    it('lets a user inside an organization open the organization route', async () => {
      const auth = useAuthStore()
      auth.account = { id: 'a-1' } as never
      auth.organizationId = 'org-1'

      await router.push('/org-only')

      expect(router.currentRoute.value.name).toBe('org-only')
    })

    it('keeps the URL of a route whose permission the user lacks', async () => {
      const auth = useAuthStore()
      auth.account = { id: 'a-1' } as never
      auth.organizationId = 'org-1'

      await router.push('/reports-only')

      expect(router.currentRoute.value.name).toBe('reports-only')
      expect(router.currentRoute.value.fullPath).toBe('/reports-only')
    })
  })

  it('keeps an unauthenticated visitor on the intended route during an API outage', async () => {
    useHealthStore().reportOutage()

    await router.push('/')

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('scrolls the window to the top on navigation, or back to the saved position on back/forward', async () => {
    // `resolve()` lacks only `redirectedFrom`, which scrollBehavior doesn't read.
    const to = router.resolve('/register') as RouteLocationNormalized
    const from = router.resolve('/login') as RouteLocationNormalized

    expect(await router.options.scrollBehavior?.(to, from, null)).toEqual({ top: 0 })
    expect(await router.options.scrollBehavior?.(to, from, { left: 0, top: 320 })).toEqual({
      left: 0,
      top: 320,
    })
  })
})
