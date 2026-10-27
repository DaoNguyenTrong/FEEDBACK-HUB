import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import RoleRow from '@/components/RoleRow.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { Permissions } from '@/lib/permissions'
import { makeRole } from '../factories/organization'
import { makeAccount } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

const t = i18n.global.t

async function render(role: ReturnType<typeof makeRole>) {
  const result = await renderComponent(RoleRow, {
    props: { role },
    attachTo: document.body,
    initialState: {
      auth: { account: makeAccount(), permissions: [Permissions.OrganizationRolesManage] },
    },
  })
  await flushPromises()
  return result
}

async function openMenu(wrapper: Awaited<ReturnType<typeof render>>['wrapper']) {
  await wrapper.get('[data-testid="role-menu"]').trigger('click')
  await flushPromises()
}

function menuOption(label: string): HTMLElement | undefined {
  return [...document.body.querySelectorAll<HTMLElement>('.n-dropdown-option-body')].find((el) =>
    el.textContent?.includes(label),
  )
}

describe('RoleRow', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
    document.body.innerHTML = ''
  })

  it.each(['vi', 'en'] as const)(
    'names system roles in the interface language and marks them view-only (%s)',
    async (locale) => {
      i18n.global.locale.value = locale
      const owner = makeRole({
        id: 'role-owner',
        name: 'Owner',
        systemKind: 'Owner',
        isSystem: true,
        canEdit: false,
        canDelete: false,
        holderCount: 2,
        permissionCodes: Object.values(Permissions),
        deleteBlockedReason: 'SystemRole',
      })

      const { wrapper } = await render(owner)

      expect(wrapper.text()).toContain(t('roles.systemKind.Owner'))
      expect(wrapper.text()).not.toContain('Owner,')
      expect(wrapper.text()).toContain(t('roles.allPermissions'))
      expect(wrapper.text()).toContain(t('roles.viewOnly'))
      expect(wrapper.text()).not.toContain(t('roles.viewOnlyMissingPermissions'))
      expect(wrapper.find('[data-testid="role-menu"]').exists()).toBe(false)
    },
  )

  it('lists the permission names of a custom role and its holder count', async () => {
    const { wrapper } = await render(
      makeRole({
        permissionCodes: [Permissions.FilesManage, Permissions.AuditLogsView],
        holderCount: 3,
      }),
    )

    expect(wrapper.text()).toContain(
      `${t('permissions.files_manage.name')}, ${t('permissions.auditlogs_view.name')}`,
    )
    expect(wrapper.text()).toContain(t('roles.holders', { n: 3 }, 3))
    expect(wrapper.text()).not.toContain(t('roles.viewOnly'))
  })

  it('says a custom role without permissions has none', async () => {
    const { wrapper } = await render(makeRole({ permissionCodes: [] }))

    expect(wrapper.text()).toContain(t('roles.noPermissions'))
  })

  it('explains a view-only custom role the caller lacks permissions for', async () => {
    const { wrapper } = await render(makeRole({ canEdit: false, canDelete: false }))

    expect(wrapper.text()).toContain(t('roles.viewOnly'))
    expect(wrapper.text()).toContain(t('roles.viewOnlyMissingPermissions'))
    expect(wrapper.find('[data-testid="role-menu"]').exists()).toBe(false)
  })

  it('names the menu button after the role so a screen reader can tell rows apart', async () => {
    const { wrapper } = await render(makeRole({ name: 'Support' }))

    expect(wrapper.get('[data-testid="role-menu"]').attributes('aria-label')).toBe(
      t('roles.actionsFor', { name: 'Support' }),
    )
  })

  it('emits edit with the role from the Edit role menu item', async () => {
    const role = makeRole()
    const { wrapper } = await render(role)

    await openMenu(wrapper)
    menuOption(t('roles.edit'))?.click()
    await flushPromises()

    expect(wrapper.emitted('edit')).toEqual([[role]])
  })

  // AC-248, AC-251
  it('disables Delete with the holders line when the role has holders', async () => {
    const { wrapper } = await render(
      makeRole({ canDelete: false, deleteBlockedReason: 'HasHolders', holderCount: 4 }),
    )

    expect(wrapper.get('[data-testid="role-delete-blocked"]').text()).toBe(
      t('roles.deleteBlockedHasHolders', { n: 4 }, 4),
    )

    await openMenu(wrapper)
    const option = menuOption(t('roles.delete'))
    expect(option).toBeDefined()
    expect(option?.classList.contains('n-dropdown-option-body--disabled')).toBe(true)
  })

  // AC-250
  it('mentions pending invitations in the delete confirmation only when there are some', async () => {
    const withInvites = await render(makeRole({ name: 'Support', pendingInvitationCount: 2 }))
    await openMenu(withInvites.wrapper)
    menuOption(t('roles.delete'))?.click()
    await flushPromises()

    const confirmText = withInvites.wrapper.get('[role="group"]').text()
    expect(confirmText).toContain(t('roles.deleteConfirmMessage', { name: 'Support' }))
    expect(confirmText).toContain(t('roles.deleteConfirmDetail', { n: 2 }, 2))
    expect(withInvites.wrapper.get('[data-testid="inline-confirm-confirm"]').text()).toBe(
      t('roles.delete'),
    )
    withInvites.wrapper.unmount()
    document.body.innerHTML = ''

    const without = await render(makeRole({ name: 'Support', pendingInvitationCount: 0 }))
    await openMenu(without.wrapper)
    menuOption(t('roles.delete'))?.click()
    await flushPromises()

    const plainText = without.wrapper.get('[role="group"]').text()
    expect(plainText).toContain(t('roles.deleteConfirmMessage', { name: 'Support' }))
    expect(plainText).not.toContain(t('roles.deleteConfirmDetail', { n: 0 }, 0))
  })

  it('shows the holders line and reloads the roles when the server says the role has holders', async () => {
    let reloaded = 0
    server.use(
      http.delete('*/api/organization/roles/*', () =>
        HttpResponse.json(
          { status: 409, code: 'RoleHasHolders', title: 'Conflict' },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
      http.get('*/api/organization/roles', () => {
        reloaded += 1
        return HttpResponse.json([])
      }),
    )
    const { wrapper } = await render(makeRole({ holderCount: 1 }))

    await openMenu(wrapper)
    menuOption(t('roles.delete'))?.click()
    await flushPromises()
    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="role-delete-blocked"]').exists()).toBe(true)
    expect(wrapper.find('[role="group"]').exists()).toBe(false)
    expect(reloaded).toBe(1)
    expect(feedback.showSuccessMessage).not.toHaveBeenCalled()
  })

  it('deletes the role and confirms with a toast', async () => {
    server.use(
      http.delete(
        '*/api/organization/roles/role-custom',
        () => new HttpResponse(null, { status: 204 }),
      ),
    )
    const { wrapper } = await render(makeRole({ name: 'Support' }))

    await openMenu(wrapper)
    menuOption(t('roles.delete'))?.click()
    await flushPromises()
    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('roles.deleted', { name: 'Support' }),
    )
  })
})
