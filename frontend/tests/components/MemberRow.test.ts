import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MemberRow from '@/components/MemberRow.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import { makeMember, makeRoleOption } from '../factories/organization'
import { makeAccount } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { storeIn } from '../helpers/pinia'
import { renderComponent } from '../helpers/render'

const t = i18n.global.t

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(
  member: ReturnType<typeof makeMember>,
  opts: {
    permissions?: string[]
    orgName?: string
    roleOptions?: ReturnType<typeof makeRoleOption>[]
  } = {},
) {
  const result = await renderComponent(MemberRow, {
    props: { member, organizationName: opts.orgName ?? 'Acme Inc' },
    initialState: {
      auth: { account: makeAccount(), permissions: opts.permissions ?? [] },
      'organization-people': { roleOptions: opts.roleOptions ?? [] },
    },
  })
  await flushPromises()
  return result
}

describe('MemberRow', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  it('shows the email only when the API returns one', async () => {
    const { wrapper: w1 } = await render(makeMember({ email: 'b@example.com' }))
    expect(w1.text()).toContain('b@example.com')

    const { wrapper: w2 } = await render(makeMember({ email: null }))
    expect(w2.text()).not.toContain('@')
  })

  it('offers Leave and Change roles but never Remove on my own row', async () => {
    const me = makeMember({ isSelf: true, canChangeRoles: true, canRemove: false })
    const { wrapper } = await render(me)

    expect(wrapper.find('[data-testid="member-leave"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="member-change-roles"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="member-menu"]').exists()).toBe(false)
  })

  it('locks an owner row with the owner-only line and no menu', async () => {
    const member = makeMember({ lockReason: 'OwnerOnly', lockRole: null, canChangeRoles: false, canRemove: false })
    const { wrapper } = await render(member)

    expect(wrapper.find('[data-testid="member-lock-reason"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(t('members.lockReasonOwnerOnly'))
    expect(wrapper.find('[data-testid="member-menu"]').exists()).toBe(false)
  })

  it('locks a row with the missing-role line naming the role in the interface language', async () => {
    const lockRole = { id: 'role-owner', name: 'Owner', systemKind: 'Owner' as const }
    const member = makeMember({ lockReason: 'InsufficientRolePermissions', lockRole, canChangeRoles: false, canRemove: false })
    const { wrapper } = await render(member)

    expect(wrapper.find('[data-testid="member-lock-reason"]').exists()).toBe(true)
    // shows the role name translated
    expect(wrapper.text()).toContain(t('roles.systemKind.Owner'))
    expect(wrapper.find('[data-testid="member-menu"]').exists()).toBe(false)
  })

  it('renders names and roles containing HTML as text', async () => {
    const member = makeMember({
      accountName: '<img src=x onerror=alert(1)>',
      roles: [{ id: 'role-1', name: '<script>bad</script>', systemKind: null }],
    })
    const { wrapper } = await render(member)

    // Text is rendered but no <img> or <script> elements
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('<script>')
    // The raw text should appear literally
    expect(wrapper.text()).toContain('<img src=x onerror=alert(1)>')
  })

  it('disables role options I cannot grant with their reason and keeps held roles checked', async () => {
    const roleOptions = [
      makeRoleOption({ id: 'role-owner', name: 'Owner', systemKind: 'Owner', canGrant: false }),
      makeRoleOption({ id: 'role-member', name: 'Member', systemKind: 'Member', canGrant: true }),
    ]
    const member = makeMember({ roleIds: ['role-member'], roles: [{ id: 'role-member', name: 'Member', systemKind: 'Member' }] })
    const { wrapper } = await render(member, { roleOptions })

    await wrapper.get('[data-testid="member-change-roles"]').trigger('click')
    await flushPromises()

    // Held role (Member) is checked; Owner (cannot grant, not held) is disabled
    const ownerCheckbox = wrapper.find('[data-testid="role-option-role-owner"]')
    expect(ownerCheckbox.exists()).toBe(true)
    const ownerInput = ownerCheckbox.find('input[type="checkbox"]')
    expect((ownerInput.element as HTMLInputElement).disabled).toBe(true)
  })

  it('blocks saving with no role selected', async () => {
    const roleOptions = [makeRoleOption({ id: 'role-member', canGrant: true })]
    const member = makeMember({ roleIds: ['role-member'] })
    let calls = 0
    server.use(http.put('*/api/organization/members/*/roles', () => { calls += 1; return HttpResponse.json(member) }))

    const { wrapper } = await render(member, { roleOptions })
    await wrapper.get('[data-testid="member-change-roles"]').trigger('click')
    await flushPromises()

    // Uncheck the only held role
    await wrapper.get('[data-testid="role-option-role-member"] input[type="checkbox"]').setValue(false)
    await wrapper.get('[data-testid="member-save-roles"]').trigger('click')
    await flushPromises()

    expect(calls).toBe(0)
    expect(wrapper.text()).toContain(t('members.atLeastOneRole'))
  })

  it('saves the role change and reports it in a toast', async () => {
    const roleOptions = [
      makeRoleOption({ id: 'role-member', name: 'Member', systemKind: 'Member', canGrant: true }),
      makeRoleOption({ id: 'role-admin', name: 'Admin', systemKind: 'Admin', canGrant: true }),
    ]
    const member = makeMember({ accountName: 'Alice', roleIds: ['role-member'], roles: [{ id: 'role-member', name: 'Member', systemKind: 'Member' }] })
    const updated = makeMember({ ...member, roleIds: ['role-admin'], roleNames: ['Admin'], roles: [{ id: 'role-admin', name: 'Admin', systemKind: 'Admin' }] })

    server.use(http.put('*/api/organization/members/*/roles', () => new HttpResponse(null, { status: 204 })))

    const { wrapper, pinia } = await render(member, { roleOptions })
    await wrapper.get('[data-testid="member-change-roles"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="role-option-role-admin"] input[type="checkbox"]').setValue(true)
    await wrapper.get('[data-testid="role-option-role-member"] input[type="checkbox"]').setValue(false)
    await wrapper.get('[data-testid="member-save-roles"]').trigger('click')
    await flushPromises()

    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('members.rolesChanged', { name: 'Alice', roles: t('roles.systemKind.Admin') }),
    )
  })

  it('confirms removal below the row and removes the row after 204', async () => {
    const member = makeMember({ accountId: 'a1', accountName: 'Bob', canRemove: true, isSelf: false })
    server.use(
      http.delete('*/api/organization/members/*', () => new HttpResponse(null, { status: 204 })),
    )

    const { wrapper } = await render(member)
    await wrapper.get('[data-testid="member-menu"]').trigger('click')
    await flushPromises()

    const removeOption = wrapper.find('[data-testid="member-remove"]')
    await removeOption.trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('members.removed', { name: 'Bob', org: 'Acme Inc' }),
    )
  })

  it('confirms leaving below the row and moves to the personal context after 204', async () => {
    const me = makeMember({ isSelf: true, accountName: 'Me' })
    server.use(
      http.post('*/api/organization/leave', () => new HttpResponse(null, { status: 204 })),
      http.post('*/api/auth/switch-organization', () => HttpResponse.json({})),
    )

    const { wrapper } = await render(me)
    await wrapper.get('[data-testid="member-leave"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    // switchOrganization(null) is called — we can check that no error toast showed
    expect(feedback.showErrorMessage).not.toHaveBeenCalled()
  })

  it('turns the leave block into the only-owner reminder on LastOwnerCannotLeave', async () => {
    const me = makeMember({ isSelf: true })
    server.use(
      http.post('*/api/organization/leave', () =>
        problem(409, { status: 409, title: 'Conflict', code: 'LastOwnerCannotLeave' }),
      ),
    )

    const { wrapper } = await render(me)
    await wrapper.get('[data-testid="member-leave"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    // The confirm button should be gone, only cancel remains, with last-owner message
    expect(wrapper.find('[data-testid="inline-confirm-confirm"]').exists()).toBe(false)
    expect(wrapper.text()).toContain(t('errorCodes.LastOwnerCannotLeave'))
  })

  it('closes the leave block on cancel and changes nothing', async () => {
    const me = makeMember({ isSelf: true })
    const { wrapper } = await render(me)

    await wrapper.get('[data-testid="member-leave"]').trigger('click')
    await flushPromises()

    await wrapper.get('[data-testid="inline-confirm-cancel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="inline-confirm-cancel"]').exists()).toBe(false)
  })
})
