import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import InvitationsView from '@/views/organization/InvitationsView.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { makeInvitation } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'
import { Permissions } from '@/lib/permissions'

const t = i18n.global.t

function pagedInvitations(
  items: ReturnType<typeof makeInvitation>[],
  total = items.length,
  page = 1,
) {
  return { items, totalCount: total, pageNumber: page, pageSize: 50, totalPages: 1 }
}

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(opts: {
  permissions?: string[]
} = {}) {
  server.use(http.get('*/api/organization/role-options', () => HttpResponse.json([])))
  const result = await renderComponent(InvitationsView, {
    global: { stubs: { teleport: true } },
    initialState: {
      auth: {
        account: makeAccount(),
        organizationId: 'org-1',
        organizationName: 'Acme Inc',
        permissions: opts.permissions ?? [Permissions.OrganizationMembersManage],
      },
    },
  })
  await flushPromises()
  return result
}

describe('InvitationsView', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  it('shows the empty state when there is no invitation', async () => {
    server.use(http.get('*/api/organization/invitations', () => HttpResponse.json(pagedInvitations([]))))

    const { wrapper } = await render()

    expect(wrapper.find('[data-testid="invitations-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(t('invitations.emptyTitle'))
  })

  it('replaces the row with the new invitation after Resend', async () => {
    const original = makeInvitation({ id: 'inv-1', status: 'Pending', canResend: true })
    const updated = makeInvitation({
      id: 'inv-2',
      email: original.email,
      status: 'Pending',
      expiresAt: '2026-12-01T00:00:00Z',
    })
    server.use(
      http.get('*/api/organization/invitations', () => HttpResponse.json(pagedInvitations([original]))),
      http.post('*/api/organization/invitations/inv-1/resend', () => HttpResponse.json(updated)),
    )

    const { wrapper } = await render()

    // Original row present
    expect(wrapper.find('[data-testid="invitation-row-inv-1"]').exists()).toBe(true)

    // Click resend
    await wrapper.find('[data-testid="invitation-resend"]').trigger('click')
    await flushPromises()

    // Original is replaced by new
    expect(wrapper.find('[data-testid="invitation-row-inv-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="invitation-row-inv-2"]').exists()).toBe(true)
  })

  it('reloads after a 404 on revoke', async () => {
    let reloadCalled = false
    const invitation = makeInvitation({ id: 'inv-1', status: 'Pending', canRevokeOrDelete: true })
    server.use(
      http.get('*/api/organization/invitations', () => {
        reloadCalled = true
        return HttpResponse.json(pagedInvitations([invitation]))
      }),
      http.delete('*/api/organization/invitations/*', () =>
        problem(404, { status: 404, code: 'ResourceNotFound', title: 'Not found' }),
      ),
    )

    const { wrapper } = await render()
    reloadCalled = false // reset after initial load

    await wrapper.find('[data-testid="invitation-revoke"]').trigger('click')
    await flushPromises()

    const confirmBtn = wrapper.find('[data-testid="inline-confirm-confirm"]')
    await confirmBtn.trigger('click')
    await flushPromises()

    expect(reloadCalled).toBe(true)
  })
})
