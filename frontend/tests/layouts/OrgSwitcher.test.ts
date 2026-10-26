import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import OrgSwitcher from '@/layouts/OrgSwitcher.vue'
import i18n from '@/plugins/i18n'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'
import { makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { makeOrganization } from '../factories/organization'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'
import { stubViewport } from '../helpers/viewport'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const t = i18n.global.t

const acme = makeOrganization({
  id: 'org-1',
  name: 'Acme',
  myRoles: [
    { id: 'r1', name: 'Owner', systemKind: 'Owner' },
    { id: 'r2', name: 'Trưởng IT', systemKind: null },
  ],
})
const globex = makeOrganization({ id: 'org-2', name: 'Globex', slug: 'globex' })
const frozen = makeOrganization({ id: 'org-3', name: 'Frozen Co', slug: 'frozen', status: false })

type Options = {
  width?: number
  auth?: Record<string, unknown>
  organizations?: unknown[]
  loaded?: boolean
}

async function render({
  width = 1280,
  auth = { organizationId: 'org-1', organizationName: 'Acme' },
  organizations = [acme, globex],
  loaded = true,
}: Options = {}) {
  stubViewport(width)
  const result = await renderComponent(OrgSwitcher, {
    initialState: { auth, organizations: { loaded, myOrganizations: organizations } },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

async function open(wrapper: Awaited<ReturnType<typeof render>>['wrapper']) {
  await wrapper.get('[data-testid="org-switcher-trigger"]').trigger('click')
  await flushPromises()
}

function row(wrapper: Awaited<ReturnType<typeof render>>['wrapper'], id: string) {
  return wrapper.get(`[data-testid="org-switcher-item-${id}"]`)
}

function mockSwitch(onBody?: (body: unknown) => void) {
  server.use(
    http.post('*/api/auth/switch-organization', async ({ request }) => {
      onBody?.(await request.json())
      return HttpResponse.json(makeAuthResponse())
    }),
  )
}

describe('OrgSwitcher', () => {
  beforeEach(() => {
    vi.mocked(reloadTo).mockClear()
    vi.mocked(reloadToLogin).mockClear()
  })
  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
  })

  it('lists only the organizations the API returns', async () => {
    server.use(http.get('*/api/organizations', () => HttpResponse.json([globex])))
    const { wrapper } = await render({ organizations: [], loaded: false })

    await open(wrapper)

    const items = wrapper.findAll('[data-testid^="org-switcher-item-"]')
    expect(items.map((item) => item.attributes('data-testid'))).toEqual([
      'org-switcher-item-org-2',
    ])
    expect(wrapper.text()).toContain(t('organizations.switcherTitle'))
  })

  it('loads the list on first open only', async () => {
    let calls = 0
    server.use(
      http.get('*/api/organizations', () => {
        calls += 1
        return HttpResponse.json([acme])
      }),
    )
    const { wrapper } = await render({ organizations: [], loaded: false })

    await open(wrapper)
    await open(wrapper)
    await open(wrapper)

    expect(calls).toBe(1)
  })

  it('shows each organization with my role names in the interface language', async () => {
    const { wrapper } = await render()
    await open(wrapper)

    expect(row(wrapper, 'org-1').text()).toContain('Acme')
    expect(row(wrapper, 'org-1').text()).toContain(
      `${t('roles.systemKind.Owner')}, Trưởng IT`,
    )
  })

  it('switches to the picked organization', async () => {
    let body: unknown
    mockSwitch((value) => (body = value))
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-2').trigger('click')
    await flushPromises()

    expect(body).toEqual({ organizationId: 'org-2' })
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it('dims a suspended organization, labels it and does not switch', async () => {
    let requests = 0
    mockSwitch(() => (requests += 1))
    const { wrapper } = await render({ organizations: [acme, frozen] })
    await open(wrapper)

    const suspended = row(wrapper, 'org-3')
    expect(suspended.attributes('aria-disabled')).toBe('true')
    expect(suspended.classes()).toContain('opacity-60')
    expect(suspended.text()).toContain(t('organizations.suspended'))
    expect(row(wrapper, 'org-1').attributes('aria-disabled')).toBeUndefined()

    await suspended.trigger('click')
    await flushPromises()

    expect(requests).toBe(0)
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('still opens when every organization is suspended', async () => {
    const { wrapper } = await render({ organizations: [frozen] })
    await open(wrapper)

    expect(wrapper.find('[role="menu"]').exists()).toBe(true)
    expect(row(wrapper, 'org-3').attributes('aria-disabled')).toBe('true')
    expect(wrapper.find('[data-testid="org-switcher-create"]').exists()).toBe(true)
  })

  it('marks the current organization as checked', async () => {
    const { wrapper } = await render()
    await open(wrapper)

    expect(row(wrapper, 'org-1').attributes('role')).toBe('menuitemradio')
    expect(row(wrapper, 'org-1').attributes('aria-checked')).toBe('true')
    expect(row(wrapper, 'org-2').attributes('aria-checked')).toBe('false')
  })

  it('shows the active organization name on the trigger and announces the menu', async () => {
    const { wrapper } = await render()
    const trigger = wrapper.get('[data-testid="org-switcher-trigger"]')

    expect(trigger.text()).toContain('Acme')
    expect(trigger.attributes('aria-haspopup')).toBe('menu')
    expect(trigger.attributes('aria-expanded')).toBe('false')
    await open(wrapper)
    expect(trigger.attributes('aria-expanded')).toBe('true')
  })

  it('shows the personal workspace label without an active organization', async () => {
    const { wrapper } = await render({ auth: { organizationId: null, organizationName: null } })

    expect(wrapper.get('[data-testid="org-switcher-trigger"]').text()).toContain(
      t('organizations.personalWorkspace'),
    )
  })

  it.each([
    [390, true],
    [834, false],
    [1280, false],
  ])('opens at %i px as a bottom sheet: %s', async (width, isSheet) => {
    const { wrapper } = await render({ width })
    await open(wrapper)

    expect(wrapper.find('[data-testid="app-sheet"]').exists()).toBe(isSheet)
    expect(wrapper.find('[role="menu"]').exists()).toBe(true)
  })

  it('asks for the create-organization sheet from its last item', async () => {
    const { wrapper, pinia } = await render()
    await open(wrapper)

    await wrapper.get('[data-testid="org-switcher-create"]').trigger('click')
    await flushPromises()

    expect(storeIn(pinia, useLayoutStore).createOrganizationOpen).toBe(true)
    expect(wrapper.get('[data-testid="org-switcher-trigger"]').attributes('aria-expanded')).toBe(
      'false',
    )
  })

  it('puts Back to welcome page directly above Create organization', async () => {
    const { wrapper } = await render()
    await open(wrapper)

    const actions = wrapper
      .get('[role="menu"]')
      .findAll('[role="menuitem"]')
      .map((item) => item.attributes('data-testid'))
    expect(actions).toEqual(['org-switcher-welcome', 'org-switcher-create'])
  })

  it('switches to no organization from Back to welcome page', async () => {
    let body: unknown
    mockSwitch((value) => (body = value))
    const { wrapper } = await render()
    await open(wrapper)

    await wrapper.get('[data-testid="org-switcher-welcome"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ organizationId: null })
    expect(reloadTo).toHaveBeenCalledOnce()
  })

  it('shows Back to welcome page in vi and en', async () => {
    const { wrapper } = await render()
    await open(wrapper)
    expect(wrapper.get('[data-testid="org-switcher-welcome"]').text()).toBe('Back to welcome page')

    i18n.global.locale.value = 'vi'
    await flushPromises()
    expect(wrapper.get('[data-testid="org-switcher-welcome"]').text()).toBe('Về trang chào')
  })

  it('keeps Back to welcome page available when every organization is suspended', async () => {
    let body: unknown
    mockSwitch((value) => (body = value))
    const { wrapper } = await render({ organizations: [frozen] })
    await open(wrapper)

    await wrapper.get('[data-testid="org-switcher-welcome"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ organizationId: null })
  })

  it('goes home without a request when already in the personal context', async () => {
    let requests = 0
    mockSwitch(() => (requests += 1))
    const { wrapper, router } = await render({
      auth: { organizationId: null, organizationName: null },
    })
    await router.push('/organizations')
    await open(wrapper)

    await wrapper.get('[data-testid="org-switcher-welcome"]').trigger('click')
    await flushPromises()

    expect(requests).toBe(0)
    expect(router.currentRoute.value.name).toBe('home')
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('closes without a request when the current organization is picked', async () => {
    let requests = 0
    mockSwitch(() => (requests += 1))
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-1').trigger('click')
    await flushPromises()

    expect(requests).toBe(0)
    expect(reloadTo).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid="org-switcher-trigger"]').attributes('aria-expanded')).toBe(
      'false',
    )
  })

  it('sends one request when a row is clicked twice before the reply', async () => {
    let requests = 0
    server.use(
      http.post('*/api/auth/switch-organization', async () => {
        requests += 1
        await new Promise((resolve) => setTimeout(resolve, 30))
        return HttpResponse.json(makeAuthResponse())
      }),
    )
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-2').trigger('click')
    await row(wrapper, 'org-2').trigger('click')
    await wrapper.get('[data-testid="org-switcher-welcome"]').trigger('click')
    await vi.waitFor(() => expect(reloadTo).toHaveBeenCalled())

    expect(requests).toBe(1)
    expect(reloadTo).toHaveBeenCalledOnce()
  })

  it('shows the mapped error in a notice and stays open when switching fails', async () => {
    server.use(
      http.post('*/api/auth/switch-organization', () =>
        HttpResponse.json(
          { status: 403, title: 'Forbidden', detail: 'Acme is suspended.', code: 'Other' },
          { status: 403 },
        ),
      ),
    )
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-2').trigger('click')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('Acme is suspended.')
    expect(wrapper.get('[data-testid="org-switcher-trigger"]').attributes('aria-expanded')).toBe(
      'true',
    )
    expect(reloadTo).not.toHaveBeenCalled()
    expect(reloadToLogin).not.toHaveBeenCalled()
  })

  it('allows another pick after a failed switch', async () => {
    let requests = 0
    server.use(
      http.post('*/api/auth/switch-organization', () => {
        requests += 1
        return HttpResponse.json({ status: 500, title: 'Boom' }, { status: 500 })
      }),
    )
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-2').trigger('click')
    await flushPromises()
    await row(wrapper, 'org-2').trigger('click')
    await flushPromises()

    expect(requests).toBe(2)
    expect(wrapper.get('[role="alert"]').text()).toContain(t('errors.requestFailed'))
  })

  it('ends the session instead of showing a notice when the switch answers 401', async () => {
    server.use(
      http.post('*/api/auth/switch-organization', () =>
        HttpResponse.json(
          { status: 401, title: 'Unauthorized', code: 'AuthenticatedUserRequired' },
          { status: 401 },
        ),
      ),
    )
    const { wrapper } = await render()
    await open(wrapper)

    await row(wrapper, 'org-2').trigger('click')
    await flushPromises()

    expect(reloadToLogin).toHaveBeenCalledOnce()
    expect(reloadTo).not.toHaveBeenCalled()
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
  })

  it('shows a notice when the list cannot be fetched and keeps the two actions', async () => {
    server.use(
      http.get('*/api/organizations', () =>
        HttpResponse.json({ status: 500, title: 'Boom' }, { status: 500 }),
      ),
    )
    const { wrapper } = await render({ organizations: [], loaded: false })

    await open(wrapper)

    expect(wrapper.get('[role="alert"]').text()).toContain(t('errors.requestFailed'))
    expect(wrapper.find('[data-testid="org-switcher-welcome"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="org-switcher-create"]').exists()).toBe(true)
  })

  describe('keyboard', () => {
    function press(key: string) {
      document.activeElement!.dispatchEvent(
        new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }),
      )
      return flushPromises()
    }

    it('focuses the first item on open, roves with the arrow keys and closes on Escape', async () => {
      const { wrapper } = await render()
      await open(wrapper)
      const trigger = wrapper.get('[data-testid="org-switcher-trigger"]')

      expect(document.activeElement).toBe(row(wrapper, 'org-1').element)
      await press('ArrowDown')
      expect(document.activeElement).toBe(row(wrapper, 'org-2').element)
      await press('End')
      expect(document.activeElement).toBe(
        wrapper.get('[data-testid="org-switcher-create"]').element,
      )

      await press('Escape')
      expect(trigger.attributes('aria-expanded')).toBe('false')
      expect(document.activeElement).toBe(trigger.element)
    })
  })
})
