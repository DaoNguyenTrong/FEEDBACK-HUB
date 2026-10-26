import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import WelcomeView from '@/views/WelcomeView.vue'
import i18n from '@/plugins/i18n'
import { reloadTo } from '@/lib/session-exit'
import { makeOrganization } from '../factories/organization'
import { makeAccount, makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { stubViewport } from '../helpers/viewport'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const t = i18n.global.t
const NOTICE_KEY = 'organization-suspended-notice'

const acme = makeOrganization({ id: 'org-1', name: 'Acme' })
const globex = makeOrganization({ id: 'org-2', name: 'Globex', slug: 'globex' })
const frozen = makeOrganization({ id: 'org-3', name: 'Frozen Co', slug: 'frozen', status: false })
const frozenToo = makeOrganization({ id: 'org-4', name: 'Chilled', slug: 'chilled', status: false })

async function render(
  organizations: unknown[],
  auth: Record<string, unknown> = {},
  width = 1280,
) {
  stubViewport(width)
  server.use(http.get('*/api/organizations', () => HttpResponse.json(organizations)))
  const result = await renderComponent(WelcomeView, {
    initialState: {
      auth: {
        account: makeAccount({ name: 'Jane Doe' }),
        organizationId: null,
        isSystemAdministrator: false,
        ...auth,
      },
    },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

describe('WelcomeView', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(reloadTo).mockClear()
  })
  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
  })

  it('greets the user and lists organizations with suspended ones dimmed and not selectable', async () => {
    const { wrapper } = await render([acme, frozen])

    expect(wrapper.text()).toContain(t('welcome.greeting', { name: 'Jane Doe' }))
    expect(wrapper.text()).toContain(t('welcome.choose'))
    const active = wrapper.get('[data-testid="welcome-org-org-1"]')
    const dimmed = wrapper.get('[data-testid="welcome-org-org-3"]')
    expect(active.attributes('aria-disabled')).toBeUndefined()
    expect(dimmed.attributes('aria-disabled')).toBe('true')
    expect(dimmed.classes()).toContain('opacity-60')
    expect(dimmed.text()).toContain(t('organizations.suspended'))
  })

  it('switches into a picked active organization', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(makeAuthResponse())
      }),
    )
    const { wrapper } = await render([acme, globex])

    await wrapper.get('[data-testid="welcome-org-org-2"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ organizationId: 'org-2' })
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it('does not switch into a suspended organization', async () => {
    let calls = 0
    server.use(
      http.post('*/api/auth/switch-organization', () => {
        calls += 1
        return HttpResponse.json(makeAuthResponse())
      }),
    )
    const { wrapper } = await render([frozen])

    await wrapper.get('[data-testid="welcome-org-org-3"]').trigger('click')
    await flushPromises()

    expect(calls).toBe(0)
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('shows the create invitation line when the user has no organization', async () => {
    const { wrapper } = await render([])

    expect(wrapper.get('[data-testid="welcome-empty"]').text()).toBe(t('welcome.noOrganizations'))
    expect(wrapper.find('[data-testid="welcome-all-suspended"]').exists()).toBe(false)
  })

  it('does not say there is no organization when loading the list failed', async () => {
    stubViewport(1280)
    server.use(http.get('*/api/organizations', () => HttpResponse.json({}, { status: 500 })))
    const { wrapper } = await renderComponent(WelcomeView, {
      initialState: { auth: { account: makeAccount({ name: 'Jane Doe' }), organizationId: null } },
      attachTo: document.body,
      global: { stubs: { teleport: true } },
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="welcome-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="welcome-empty"]').exists()).toBe(false)
  })

  it('shows the contact line when every organization is suspended', async () => {
    const { wrapper } = await render([frozen, frozenToo])

    expect(wrapper.get('[data-testid="welcome-all-suspended"]').text()).toContain(
      t('welcome.allSuspended', { names: 'Frozen Co, Chilled' }),
    )
    expect(wrapper.find('[data-testid="welcome-empty"]').exists()).toBe(false)
  })

  it('shows the just-suspended notice once with the organization name', async () => {
    sessionStorage.setItem(NOTICE_KEY, 'Acme')
    const first = await render([frozen])

    expect(first.wrapper.get('[data-testid="welcome-suspended-notice"]').text()).toContain(
      t('welcome.justSuspended', { name: 'Acme' }),
    )
    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()

    first.wrapper.unmount()
    const second = await render([frozen])
    expect(second.wrapper.find('[data-testid="welcome-suspended-notice"]').exists()).toBe(false)
  })

  it('opens the create-organization sheet', async () => {
    const { wrapper, pinia } = await render([acme])

    await wrapper.get('[data-testid="welcome-create"]').trigger('click')
    await flushPromises()

    const layout = pinia.state.value.layout as { createOrganizationOpen: boolean }
    expect(layout.createOrganizationOpen).toBe(true)
    expect(wrapper.text()).toContain(t('welcome.createHint'))
  })

  it('shows the profile panel from the user menu', async () => {
    const { wrapper } = await render([acme])

    expect(wrapper.find('[data-testid="app-sheet"]').exists()).toBe(false)

    await wrapper.get('[data-testid="app-header-user-menu"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="user-menu-profile"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="app-sheet"]').attributes('aria-label')).toBe(
      t('profile.title'),
    )
  })

  describe('Organizations link', () => {
    it('shows the link to a system administrator without any organization', async () => {
      const { wrapper } = await render([], { isSystemAdministrator: true })

      expect(wrapper.get('[data-testid="welcome-system-organizations"]').text()).toBe(
        t('welcome.systemOrganizations'),
      )
    })

    it('shows the link to a system administrator who has organizations', async () => {
      const { wrapper } = await render([acme], { isSystemAdministrator: true })

      expect(wrapper.find('[data-testid="welcome-system-organizations"]').exists()).toBe(true)
    })

    it('hides the link from a user who is not a system administrator', async () => {
      const { wrapper } = await render([acme])

      expect(wrapper.find('[data-testid="welcome-system-organizations"]').exists()).toBe(false)
    })

    it('opens /system/organizations from the link', async () => {
      const { wrapper, router } = await render([acme], { isSystemAdministrator: true })

      await wrapper.get('[data-testid="welcome-system-organizations"]').trigger('click')
      await flushPromises()

      expect(router.currentRoute.value.path).toBe('/system/organizations')
    })
  })
})
