import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import RolesView from '@/views/organization/RolesView.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { Permissions } from '@/lib/permissions'
import { makePermission, makeRole } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'
import { stubViewport } from '../../helpers/viewport'

const t = i18n.global.t

const ownerRole = makeRole({
  id: 'role-owner',
  name: 'Owner',
  systemKind: 'Owner',
  isSystem: true,
  canEdit: false,
  canDelete: false,
  deleteBlockedReason: 'SystemRole',
  permissionCodes: Object.values(Permissions),
})

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render() {
  const result = await renderComponent(RolesView, {
    global: { stubs: { teleport: true } },
    attachTo: document.body,
    initialState: {
      auth: {
        account: makeAccount(),
        organizationId: 'org-1',
        organizationName: 'Acme Inc',
        permissions: Object.values(Permissions),
      },
    },
  })
  await flushPromises()
  return result
}

function useCatalog() {
  server.use(
    http.get('*/api/permissions', () =>
      HttpResponse.json(Object.values(Permissions).map(makePermission)),
    ),
  )
}

describe('RolesView', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
    useCatalog()
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  it('lists the roles under the Roles tab', async () => {
    server.use(
      http.get('*/api/organization/roles', () =>
        HttpResponse.json([ownerRole, makeRole({ id: 'r1', name: 'Support' })]),
      ),
    )

    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="tab-roles"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.find('[data-testid="role-row-role-owner"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="role-row-r1"]').text()).toContain('Support')
  })

  it.each([
    [390, 'roles.createShort'],
    [1280, 'roles.create'],
  ])('labels the header action for a %ipx screen with %s', async (width, key) => {
    stubViewport(width)
    server.use(http.get('*/api/organization/roles', () => HttpResponse.json([ownerRole])))

    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="create-role-btn"]').text()).toBe(t(key))
  })

  // AC-292
  it('creates a role and adds it to the list', async () => {
    server.use(
      http.get('*/api/organization/roles', () => HttpResponse.json([ownerRole])),
      http.post('*/api/organization/roles', () =>
        HttpResponse.json(makeRole({ id: 'new', name: 'Support' })),
      ),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="create-role-btn"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="role-name"]').setValue('Support')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="role-row-new"]').exists()).toBe(true)
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('roles.created', { name: 'Support' }),
    )
  })

  it('opens the sheet with the role from the Edit role menu item and saves it', async () => {
    server.use(
      http.get('*/api/organization/roles', () =>
        HttpResponse.json([ownerRole, makeRole({ id: 'r1', name: 'Support' })]),
      ),
      http.put('*/api/organization/roles/r1', () =>
        HttpResponse.json(makeRole({ id: 'r1', name: 'Helpers' })),
      ),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="role-row-r1"] [data-testid="role-menu"]').trigger('click')
    await flushPromises()
    ;[...document.body.querySelectorAll<HTMLElement>('.n-dropdown-option-body')]
      .find((el) => el.textContent?.includes(t('roles.edit')))
      ?.click()
    await flushPromises()

    const name = wrapper.get('[data-testid="role-name"]')
    expect((name.element as HTMLInputElement).value).toBe('Support')
    await name.setValue('Helpers')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-row-r1"]').text()).toContain('Helpers')
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(t('roles.saved', { name: 'Helpers' }))
  })

  it('deletes a role after the inline confirmation', async () => {
    server.use(
      http.get('*/api/organization/roles', () =>
        HttpResponse.json([ownerRole, makeRole({ id: 'r1', name: 'Support' })]),
      ),
      http.delete('*/api/organization/roles/r1', () => new HttpResponse(null, { status: 204 })),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="role-row-r1"] [data-testid="role-menu"]').trigger('click')
    await flushPromises()
    ;[...document.body.querySelectorAll<HTMLElement>('.n-dropdown-option-body')]
      .find((el) => el.textContent?.includes(t('roles.delete')))
      ?.click()
    await flushPromises()
    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="role-row-r1"]').exists()).toBe(false)
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('roles.deleted', { name: 'Support' }),
    )
  })

  // AC-300, AC-313
  it.each([
    [404, 'ResourceNotFound', 'notFoundTitle'],
    [403, 'OrganizationAccessDenied', 'forbiddenTitle'],
  ])(
    'shows the not-found or no-permission state when the role list fails to load (%i)',
    async (status, code, titleKey) => {
      server.use(
        http.get('*/api/organization/roles', () =>
          problem(status, { status, code, title: 'Error' }),
        ),
      )

      const { wrapper } = await render()

      expect(wrapper.text()).toContain(t(`accessProblem.${titleKey}`))
      expect(wrapper.find('[data-testid="role-row-role-owner"]').exists()).toBe(false)
    },
  )
})
