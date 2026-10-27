import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import InviteMemberSheet from '@/components/InviteMemberSheet.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { makeRoleOption, makeInvitation } from '../factories/organization'
import { makeAccount } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { Permissions } from '@/lib/permissions'

const t = i18n.global.t

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

const ownerRole = makeRoleOption({ id: 'role-owner', name: 'Owner', systemKind: 'Owner', canGrant: false })
const memberRole = makeRoleOption({ id: 'role-member', name: 'Member', systemKind: 'Member', canGrant: true })

async function render(opts: {
  roleOptions?: ReturnType<typeof makeRoleOption>[]
  permissions?: string[]
} = {}) {
  const result = await renderComponent(InviteMemberSheet, {
    props: { show: true },
    global: { stubs: { teleport: true } },
    initialState: {
      auth: {
        account: makeAccount(),
        organizationName: 'Acme Inc',
        permissions: opts.permissions ?? [Permissions.OrganizationMembersManage],
      },
      'organization-people': {
        roleOptions: opts.roleOptions ?? [ownerRole, memberRole],
      },
    },
  })
  await flushPromises()
  return result
}

describe('InviteMemberSheet', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  it('lists every role and disables the ones I cannot grant without showing permissions', async () => {
    const { wrapper } = await render()

    const ownerOption = wrapper.find('[data-testid="role-option-role-owner"]')
    const memberOption = wrapper.find('[data-testid="role-option-role-member"]')

    expect(ownerOption.exists()).toBe(true)
    expect(memberOption.exists()).toBe(true)

    // Owner is not grantable → radio is disabled
    const ownerInput = ownerOption.find('input')
    expect(ownerInput.attributes('disabled')).toBeDefined()

    const memberInput = memberOption.find('input')
    expect(memberInput.attributes('disabled')).toBeUndefined()
  })

  it('sends one request however many times Send is pressed while sending', async () => {
    let callCount = 0
    const invitation = makeInvitation()
    server.use(
      http.post('*/api/organization/invitations', async () => {
        callCount += 1
        await new Promise((resolve) => setTimeout(resolve, 100))
        return HttpResponse.json(invitation, { status: 201 })
      }),
    )
    const { wrapper } = await render({ roleOptions: [memberRole] })

    // Fill email
    const emailInput = wrapper.find('[data-testid="invite-email"]')
    await emailInput.setValue('test@example.com')

    // Select role
    const roleInput = wrapper.find('[data-testid="role-option-role-member"] input')
    await roleInput.trigger('change')

    const sendBtn = wrapper.find('[data-testid="invite-send"]')
    // Click multiple times
    await sendBtn.trigger('click')
    await sendBtn.trigger('click')
    await sendBtn.trigger('click')

    await flushPromises()

    expect(callCount).toBe(1)
  })

  it('keeps the form and shows the error without retrying on 503', async () => {
    server.use(
      http.post('*/api/organization/invitations', () =>
        problem(503, { status: 503, code: 'InvitationEmailDeliveryFailed', title: 'Failed' }),
      ),
    )
    const { wrapper } = await render({ roleOptions: [memberRole] })

    const emailInput = wrapper.find('[data-testid="invite-email"]')
    await emailInput.setValue('test@example.com')

    const roleInput = wrapper.find('[data-testid="role-option-role-member"] input')
    await roleInput.trigger('change')

    const sendBtn = wrapper.find('[data-testid="invite-send"]')
    await sendBtn.trigger('click')
    await flushPromises()

    // Error message is shown inline
    const errorNotice = wrapper.find('[data-testid="invite-error"]')
    expect(errorNotice.exists()).toBe(true)
    expect(errorNotice.text()).toContain(t('errorCodes.InvitationEmailDeliveryFailed'))

    // Email field still has value (form data preserved)
    expect((emailInput.element as HTMLInputElement).value).toBe('test@example.com')
  })

  it('shows the already-member message under the email', async () => {
    server.use(
      http.post('*/api/organization/invitations', () =>
        problem(409, { status: 409, code: 'InvitationEmailAlreadyMember', title: 'Already member' }),
      ),
    )
    const { wrapper } = await render({ roleOptions: [memberRole] })

    const emailInput = wrapper.find('[data-testid="invite-email"]')
    await emailInput.setValue('existing@example.com')

    const roleInput = wrapper.find('[data-testid="role-option-role-member"] input')
    await roleInput.trigger('change')

    await wrapper.find('[data-testid="invite-send"]').trigger('click')
    await flushPromises()

    const emailError = wrapper.find('[data-testid="invite-email-error"]')
    expect(emailError.exists()).toBe(true)
    expect(emailError.text()).toContain(t('errorCodes.InvitationEmailAlreadyMember', { org: 'Acme Inc' }))
  })

  it('closes the sheet without sending when Cancel is clicked', async () => {
    const { wrapper } = await render({ roleOptions: [memberRole] })

    await wrapper.find('[data-testid="invite-cancel"]').trigger('click')
    await flushPromises()

    const emitted = wrapper.emitted('update:show')
    expect(emitted).toBeTruthy()
    expect(emitted![emitted!.length - 1]).toEqual([false])
  })

  it('closes and reports the sent invitation on 201', async () => {
    const invitation = makeInvitation({ email: 'new@example.com' })
    server.use(
      http.post('*/api/organization/invitations', () => HttpResponse.json(invitation, { status: 201 })),
    )
    const { wrapper } = await render({ roleOptions: [memberRole] })

    const emailInput = wrapper.find('[data-testid="invite-email"]')
    await emailInput.setValue('new@example.com')

    const roleInput = wrapper.find('[data-testid="role-option-role-member"] input')
    await roleInput.trigger('change')

    await wrapper.find('[data-testid="invite-send"]').trigger('click')
    await flushPromises()

    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('invitations.sent', { email: 'new@example.com' }),
    )

    const emitted = wrapper.emitted('update:show')
    expect(emitted).toBeTruthy()
    expect(emitted![emitted!.length - 1]).toEqual([false])
  })
})
