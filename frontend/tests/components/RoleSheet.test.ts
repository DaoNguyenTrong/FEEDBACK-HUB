import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import RoleSheet from '@/components/RoleSheet.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { Permissions } from '@/lib/permissions'
import { makePermission, makeRole } from '../factories/organization'
import { makeAccount } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

const t = i18n.global.t

const catalog = Object.values(Permissions).map(makePermission)

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(opts: { role?: ReturnType<typeof makeRole>; permissions?: string[] } = {}) {
  const result = await renderComponent(RoleSheet, {
    props: { show: true, role: opts.role },
    global: { stubs: { teleport: true } },
    initialState: {
      auth: {
        account: makeAccount(),
        permissions: opts.permissions ?? Object.values(Permissions),
      },
      'organization-people': { permissionCatalog: catalog },
    },
  })
  await flushPromises()
  return result
}

async function typeName(wrapper: Awaited<ReturnType<typeof render>>['wrapper'], value: string) {
  await wrapper.get('[data-testid="role-name"]').setValue(value)
}

describe('RoleSheet', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  // AC-156, AC-294, AC-327
  it('shows each permission with its description and locks the ones I do not have', async () => {
    const { wrapper } = await render({
      permissions: [Permissions.OrganizationRolesManage, Permissions.FilesManage],
    })

    for (const code of Object.values(Permissions)) {
      const option = wrapper.get(`[data-testid="permission-option-${code}"]`)
      expect(option.text()).toContain(t(`permissions.${code.replaceAll('.', '_')}.name`))
      expect(option.text()).toContain(t(`permissions.${code.replaceAll('.', '_')}.description`))
    }

    const held = wrapper.get(`[data-testid="permission-option-${Permissions.FilesManage}"] input`)
    const lacking = wrapper.get(
      `[data-testid="permission-option-${Permissions.ApiKeysManage}"] input`,
    )
    expect(held.attributes('disabled')).toBeUndefined()
    expect(lacking.attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain(t('roles.grantableOnly'))
  })

  it('creates a role with the ticked permissions', async () => {
    let body: unknown
    server.use(
      http.post('*/api/organization/roles', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(makeRole({ name: 'Support' }))
      }),
    )
    const { wrapper } = await render()

    await typeName(wrapper, '  Support ')
    await wrapper
      .get(`[data-testid="permission-option-${Permissions.FilesManage}"] input`)
      .setValue(true)
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ name: 'Support', permissionCodes: [Permissions.FilesManage] })
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('roles.created', { name: 'Support' }),
    )
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })

  it('opens an existing role with its name and permissions ticked and saves with Save', async () => {
    let body: unknown
    server.use(
      http.put('*/api/organization/roles/role-custom', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(makeRole({ name: 'Support 2' }))
      }),
    )
    const { wrapper } = await render({
      role: makeRole({ name: 'Support', permissionCodes: [Permissions.FilesManage] }),
    })

    expect((wrapper.get('[data-testid="role-name"]').element as HTMLInputElement).value).toBe(
      'Support',
    )
    expect(
      (
        wrapper.get(`[data-testid="permission-option-${Permissions.FilesManage}"] input`)
          .element as HTMLInputElement
      ).checked,
    ).toBe(true)
    expect(wrapper.get('[data-testid="role-submit"]').text()).toBe(t('common.save'))

    await typeName(wrapper, 'Support 2')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ name: 'Support 2', permissionCodes: [Permissions.FilesManage] })
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('roles.saved', { name: 'Support 2' }),
    )
  })

  // AC-290, AC-326
  it('shows the duplicate-name message under the name', async () => {
    server.use(
      http.post('*/api/organization/roles', () =>
        problem(409, { status: 409, code: 'OrganizationRoleNameAlreadyExists', title: 'Conflict' }),
      ),
    )
    const { wrapper } = await render()

    await typeName(wrapper, 'Support')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-name-error"]').text()).toBe(
      t('errorCodes.OrganizationRoleNameAlreadyExists'),
    )
    expect(wrapper.find('[data-testid="role-error"]').exists()).toBe(false)
    expect(wrapper.emitted('update:show')).toBeUndefined()
  })

  // AC-291
  it('shows the reserved-name message under the name', async () => {
    server.use(
      http.post('*/api/organization/roles', () =>
        problem(409, { status: 409, code: 'RoleNameReservedForSystemRole', title: 'Conflict' }),
      ),
    )
    const { wrapper } = await render()

    await typeName(wrapper, 'Admin')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-name-error"]').text()).toBe(
      t('errorCodes.RoleNameReservedForSystemRole'),
    )
  })

  // AC-287
  it.each(['', '   '])('requires a name before calling the server (%j)', async (name) => {
    let called = false
    server.use(
      http.post('*/api/organization/roles', () => {
        called = true
        return HttpResponse.json(makeRole())
      }),
    )
    const { wrapper } = await render()

    await typeName(wrapper, name)
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-name-error"]').text()).toBe(t('roles.nameRequired'))
    expect(called).toBe(false)
  })

  // AC-288, AC-289
  it('limits the name to 100 characters and accepts exactly 100', async () => {
    let body: { name: string } | undefined
    server.use(
      http.post('*/api/organization/roles', async ({ request }) => {
        body = (await request.json()) as { name: string }
        return HttpResponse.json(makeRole())
      }),
    )
    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="role-name"]').attributes('maxlength')).toBe('100')

    await typeName(wrapper, 'a'.repeat(100))
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(body?.name).toHaveLength(100)
    expect(wrapper.find('[data-testid="role-name-error"]').exists()).toBe(false)
  })

  it('shows the too-long message under the name when the server rejects the length', async () => {
    server.use(
      http.post('*/api/organization/roles', () =>
        problem(400, {
          status: 400,
          code: 'ValidationFailed',
          title: 'Bad Request',
          errors: { Name: ['FieldMaxLength'] },
        }),
      ),
    )
    const { wrapper } = await render()

    await typeName(wrapper, 'Support')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-name-error"]').text()).toBe(
      t('errorCodes.FieldMaxLength'),
    )
  })

  it.each([
    'InsufficientRolePermissions',
    'OrganizationSystemRoleImmutable',
    'UnknownPermissionCode',
  ])('shows %s as a notice inside the sheet and keeps it open', async (code) => {
    server.use(
      http.post('*/api/organization/roles', () =>
        problem(400, { status: 400, code, title: 'Error' }),
      ),
    )
    const { wrapper } = await render()

    await typeName(wrapper, 'Support')
    await wrapper.get('[data-testid="role-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="role-error"]').text()).toBe(t(`errorCodes.${code}`))
    expect(wrapper.find('[data-testid="role-name-error"]').exists()).toBe(false)
    expect(wrapper.emitted('update:show')).toBeUndefined()
  })
})
