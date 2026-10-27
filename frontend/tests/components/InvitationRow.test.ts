import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import InvitationRow from '@/components/InvitationRow.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { makeInvitation, makeRoleRef } from '../factories/organization'
import { makeAccount } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { Permissions } from '@/lib/permissions'
import { formatDateTime } from '@/lib/date-format'

const t = i18n.global.t

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(
  invitation: ReturnType<typeof makeInvitation>,
  opts: {
    permissions?: string[]
  } = {},
) {
  const result = await renderComponent(InvitationRow, {
    props: { invitation },
    initialState: {
      auth: {
        account: makeAccount(),
        permissions: opts.permissions ?? [Permissions.OrganizationMembersManage],
      },
      'organization-people': {},
    },
  })
  await flushPromises()
  return result
}

describe('InvitationRow', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  it('shows a pending invitation with its role and expiry', async () => {
    const invitation = makeInvitation({
      email: 'pending@example.com',
      status: 'Pending',
      expiresAt: '2026-10-30T07:30:00Z',
    })
    const { wrapper } = await render(invitation)

    expect(wrapper.text()).toContain('pending@example.com')
    expect(wrapper.text()).toContain(t('roles.systemKind.Member'))
    // Shows expiry time
    const expectedTime = formatDateTime('2026-10-30T07:30:00Z', 'vi')
    expect(wrapper.text()).toContain(expectedTime)
  })

  it('labels an expired invitation and offers Resend and Delete', async () => {
    const invitation = makeInvitation({
      status: 'Expired',
      expiresAt: '2026-10-01T00:00:00Z',
    })
    const { wrapper } = await render(invitation)

    // Shows expired status label
    expect(wrapper.find('[data-testid="invitation-expired-label"]').exists()).toBe(true)
    // Offers resend
    expect(wrapper.find('[data-testid="invitation-resend"]').exists()).toBe(true)
    // Offers delete
    expect(wrapper.find('[data-testid="invitation-delete"]').exists()).toBe(true)
  })

  it('deletes an expired invitation without a confirmation step', async () => {
    let deleted = false
    server.use(
      http.delete('*/api/organization/invitations/*', () => {
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const invitation = makeInvitation({ id: 'inv-1', status: 'Expired' })
    const { wrapper } = await render(invitation)

    await wrapper.find('[data-testid="invitation-delete"]').trigger('click')
    await flushPromises()

    expect(deleted).toBe(true)
    // Row emits removed
    expect(wrapper.emitted('removed')).toBeTruthy()
  })

  it('confirms revoking below the row', async () => {
    let revoked = false
    server.use(
      http.delete('*/api/organization/invitations/*', () => {
        revoked = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const invitation = makeInvitation({ id: 'inv-1', status: 'Pending', canRevokeOrDelete: true })
    const { wrapper } = await render(invitation)

    // Click revoke
    await wrapper.find('[data-testid="invitation-revoke"]').trigger('click')
    await flushPromises()

    // Confirmation appears
    const confirm = wrapper.find('[data-testid="invitation-revoke-confirm"]')
    expect(confirm.exists()).toBe(true)

    // Confirm the revoke
    const confirmBtn = confirm.find('[data-testid="inline-confirm-confirm"]')
    await confirmBtn.trigger('click')
    await flushPromises()

    expect(revoked).toBe(true)
  })

  it('shows Sending… and locks the row while resending', async () => {
    let resolveFn: (() => void) | undefined
    const pendingPromise = new Promise<void>((resolve) => { resolveFn = resolve })
    const newInvitation = makeInvitation({ id: 'inv-2' })

    server.use(
      http.post('*/api/organization/invitations/*/resend', async () => {
        await pendingPromise
        return HttpResponse.json(newInvitation)
      }),
    )
    const invitation = makeInvitation({ id: 'inv-1', status: 'Pending', canResend: true })
    const { wrapper } = await render(invitation)

    await wrapper.find('[data-testid="invitation-resend"]').trigger('click')
    await flushPromises()

    // Button shows "Sending..." and is disabled
    const resendBtn = wrapper.find('[data-testid="invitation-resend"]')
    expect(resendBtn.text()).toContain(t('invitations.resending'))
    expect(resendBtn.attributes('disabled')).toBeDefined()

    // Resolve
    resolveFn!()
    await flushPromises()
  })

  it('shows the lock line instead of the actions', async () => {
    const ownerRole = makeRoleRef({ id: 'role-owner', name: 'Owner', systemKind: 'Owner' })
    const invitation = makeInvitation({
      lockReason: 'OwnerOnly',
      role: ownerRole,
      canRevokeOrDelete: false,
      canResend: false,
    })
    const { wrapper } = await render(invitation)

    expect(wrapper.find('[data-testid="invitation-lock-reason"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="invitation-revoke"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invitation-resend"]').exists()).toBe(false)
  })
})
