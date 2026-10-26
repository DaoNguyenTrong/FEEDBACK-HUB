import { DOMWrapper, flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import OrganizationSettingsView from '@/views/organization/OrganizationSettingsView.vue'
import WelcomeView from '@/views/WelcomeView.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { reloadTo } from '@/lib/session-exit'
import { useAuthStore } from '@/stores/auth'
import { useOrganizationContextStore } from '@/stores/organization-context'
import { useOrganizationsStore } from '@/stores/organizations'
import { makeCurrentOrganization, makeOrganization } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { storeIn } from '../../helpers/pinia'
import { renderComponent } from '../../helpers/render'
import { stubViewport } from '../../helpers/viewport'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const t = i18n.global.t
const NOTICE_KEY = 'organization-suspended-notice'

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(auth: Record<string, unknown> = {}, current = makeCurrentOrganization()) {
  stubViewport(1280)
  server.use(http.get('*/api/organization', () => HttpResponse.json(current)))
  const result = await renderComponent(OrganizationSettingsView, {
    initialState: {
      auth: {
        account: makeAccount(),
        organizationId: 'org-1',
        organizationName: 'Acme Inc',
        isSystemAdministrator: false,
        ...auth,
      },
      organizations: { myOrganizations: [makeOrganization()] },
    },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

type Wrapper = Awaited<ReturnType<typeof render>>['wrapper']

const find = (testid: string) => document.body.querySelector<HTMLElement>(`[data-testid="${testid}"]`)
const reasonBox = () => new DOMWrapper(find('deactivate-org-reason') as HTMLTextAreaElement)

async function openDialog(wrapper: Wrapper) {
  await wrapper.get('[data-testid="org-settings-deactivate-open"]').trigger('click')
  await flushPromises()
}

async function confirmDeactivation() {
  await new DOMWrapper(find('deactivate-org-submit')!).trigger('click')
  await flushPromises()
}

describe('OrganizationSettingsView', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(reloadTo).mockClear()
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    document.body.innerHTML = ''
    vi.restoreAllMocks()
  })

  it('shows the name, the read-only identifier and the deactivate section', async () => {
    const { wrapper } = await render()

    expect(wrapper.get<HTMLInputElement>('[data-testid="org-settings-name"]').element.value).toBe(
      'Acme Inc',
    )
    expect(wrapper.get('[data-testid="org-settings-slug"]').text()).toBe('acme')
    expect(wrapper.text()).toContain(t('organizationSettings.slugFixed'))
    expect(wrapper.find('[data-testid="org-settings-deactivate"]').exists()).toBe(true)
  })

  it('renames the organization and shows the new name in the toast', async () => {
    let body: unknown
    server.use(
      http.patch('*/api/organization', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(makeCurrentOrganization({ name: 'Globex' }))
      }),
    )
    const { wrapper, pinia } = await render()

    await wrapper.get('[data-testid="org-settings-name"]').setValue('  Globex ')
    await wrapper.get('[data-testid="org-settings-save"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ name: 'Globex' })
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('organizationSettings.renamed', { name: 'Globex' }),
    )
    expect(storeIn(pinia, useAuthStore).organizationName).toBe('Globex')
    expect(storeIn(pinia, useOrganizationsStore).myOrganizations[0]!.name).toBe('Globex')
  })

  it('rejects an empty name under the field without calling the server', async () => {
    let calls = 0
    server.use(
      http.patch('*/api/organization', () => {
        calls += 1
        return HttpResponse.json(makeCurrentOrganization())
      }),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="org-settings-name"]').setValue('   ')
    await wrapper.get('[data-testid="org-settings-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="org-settings-name-error"]').text()).toBe(
      t('organizations.nameRequired'),
    )
    expect(calls).toBe(0)
  })

  it('shows OrganizationNameRequired from the server under the field', async () => {
    server.use(
      http.patch('*/api/organization', () =>
        problem(400, { status: 400, title: 'Bad', code: 'OrganizationNameRequired' }),
      ),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="org-settings-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="org-settings-name-error"]').text()).toBe(
      t('errorCodes.OrganizationNameRequired'),
    )
  })

  it('shows ValidationFailed per field', async () => {
    server.use(
      http.patch('*/api/organization', () =>
        problem(400, {
          status: 400,
          title: 'Validation',
          code: 'ValidationFailed',
          errors: { Name: ['Too long'] },
        }),
      ),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="org-settings-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="org-settings-name-error"]').text()).toBe('Too long')
  })

  it('opens the deactivate dialog and moves to the personal context after 204', async () => {
    let deactivateBody: unknown
    let switchBody: unknown
    server.use(
      http.post('*/api/organization/deactivate', async ({ request }) => {
        deactivateBody = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        switchBody = await request.json()
        return HttpResponse.json({})
      }),
    )
    const { wrapper } = await render()

    await openDialog(wrapper)
    expect(find('deactivate-org-dialog')).not.toBeNull()
    await confirmDeactivation()

    expect(deactivateBody).toEqual({ reason: null })
    expect(switchBody).toEqual({ organizationId: null })
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
  })

  it('goes to the welcome page after 204 without storing a suspended notice', async () => {
    server.use(
      http.post('*/api/organization/deactivate', () => new HttpResponse(null, { status: 204 })),
      http.post('*/api/auth/switch-organization', () => HttpResponse.json({})),
    )
    const { wrapper, pinia } = await render()

    await openDialog(wrapper)
    await confirmDeactivation()

    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()

    // The poll's 403 lands right after the deactivation (R17): our own exit, so no notice either.
    await storeIn(pinia, useOrganizationContextStore).handleAccessProblem({
      status: 403,
      title: 'Forbidden',
      code: 'OrganizationSuspended',
      organizationName: 'Acme Inc',
    })
    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
  })

  it('shows the welcome page without the just-suspended notice after deactivating the current organization', async () => {
    server.use(
      http.post('*/api/organization/deactivate', () => new HttpResponse(null, { status: 204 })),
      http.post('*/api/auth/switch-organization', () => HttpResponse.json({})),
      http.get('*/api/organizations', () =>
        HttpResponse.json([makeOrganization({ status: false })]),
      ),
    )
    const { wrapper } = await render()
    await openDialog(wrapper)
    await confirmDeactivation()

    const welcome = await renderComponent(WelcomeView, {
      initialState: { auth: { account: makeAccount(), organizationId: null } },
      attachTo: document.body,
      global: { stubs: { teleport: true } },
    })
    await flushPromises()

    expect(welcome.wrapper.find('[data-testid="welcome-suspended-notice"]').exists()).toBe(false)
    expect(welcome.wrapper.find('[data-testid="welcome-all-suspended"]').exists()).toBe(true)
    const row = welcome.wrapper.get('[data-testid="welcome-org-org-1"]')
    expect(row.attributes('aria-disabled')).toBe('true')
    expect(row.text()).toContain(t('organizations.suspended'))
  })

  it('stays on the page and ends the own-exit state when the deactivation fails', async () => {
    server.use(
      http.post('*/api/organization/deactivate', () =>
        problem(403, { status: 403, title: 'Forbidden', code: 'InsufficientRolePermissions' }),
      ),
      http.get('*/api/organizations', () => HttpResponse.json([makeOrganization()])),
    )
    const { wrapper, pinia } = await render()

    await openDialog(wrapper)
    await confirmDeactivation()

    expect(find('deactivate-org-error')?.textContent).toContain(
      t('errorCodes.InsufficientRolePermissions'),
    )
    expect(reloadTo).not.toHaveBeenCalled()

    // After the failure a real suspension by someone else is handled again.
    server.use(http.post('*/api/auth/switch-organization', () => HttpResponse.json({})))
    await storeIn(pinia, useOrganizationContextStore).handleAccessProblem({
      status: 403,
      title: 'Forbidden',
      code: 'OrganizationSuspended',
      organizationName: 'Acme Inc',
    })
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
    expect(sessionStorage.getItem(NOTICE_KEY)).toBe('Acme Inc')
  })

  it('shows DeactivationReasonRequired under the reason field', async () => {
    server.use(
      http.post('*/api/organization/deactivate', () =>
        problem(400, { status: 400, title: 'Bad', code: 'DeactivationReasonRequired' }),
      ),
    )
    const { wrapper } = await render()

    await openDialog(wrapper)
    await confirmDeactivation()

    expect(find('deactivate-org-reason-error')?.textContent).toContain(
      t('errorCodes.DeactivationReasonRequired'),
    )
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('keeps the reason field and hides the owner email hint for both a member and a system administrator', async () => {
    for (const isSystemAdministrator of [false, true]) {
      document.body.innerHTML = ''
      const { wrapper } = await render({ isSystemAdministrator })
      await openDialog(wrapper)

      expect(find('deactivate-org-reason')).not.toBeNull()
      expect(document.body.textContent).not.toMatch(/email/i)
      expect(document.body.textContent?.includes(t('deactivateOrganization.optional'))).toBe(
        !isSystemAdministrator,
      )
    }
  })

  it('requires a reason and sends it when the viewer is a system administrator', async () => {
    let body: unknown
    server.use(
      http.post('*/api/organization/deactivate', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('*/api/auth/switch-organization', () => HttpResponse.json({})),
    )
    const { wrapper } = await render({ isSystemAdministrator: true })

    await openDialog(wrapper)
    await confirmDeactivation()
    expect(body).toBeUndefined()

    await reasonBox().setValue('Abuse')
    await confirmDeactivation()
    expect(body).toEqual({ reason: 'Abuse' })
  })

  it.each([
    [404, 'ResourceNotFound', 'accessProblem.notFoundTitle'],
    [403, 'OrganizationAccessDenied', 'accessProblem.forbiddenTitle'],
  ])('shows the generic state when loading fails with %i', async (status, code, titleKey) => {
    stubViewport(1280)
    server.use(
      http.get('*/api/organization', () =>
        problem(status, { status, title: 'x', code, detail: 'Acme Inc secret detail' }),
      ),
    )
    const { wrapper } = await renderComponent(OrganizationSettingsView, {
      initialState: { auth: { organizationId: 'org-1' } },
      global: { stubs: { teleport: true } },
    })
    await flushPromises()

    expect(wrapper.text()).toContain(t(titleKey))
    expect(wrapper.text()).not.toContain('secret detail')
    expect(wrapper.find('[data-testid="org-settings-info"]').exists()).toBe(false)
  })
})
