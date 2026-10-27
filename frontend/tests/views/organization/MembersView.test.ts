import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MembersView from '@/views/organization/MembersView.vue'
import i18n from '@/plugins/i18n'
import * as feedback from '@/lib/feedback'
import { makeMember } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'

const t = i18n.global.t

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function pagedMembers(
  items: ReturnType<typeof makeMember>[],
  total = items.length,
  page = 1,
) {
  return { items, totalCount: total, pageNumber: page, pageSize: 50, totalPages: Math.ceil(total / 50) }
}

async function render(
  opts: {
    memberResponse?: ReturnType<typeof HttpResponse.json>
    permissions?: string[]
  } = {},
) {
  const members = Array.from({ length: 3 }, (_, i) => makeMember({ accountId: `a${i}` }))
  if (opts.memberResponse === undefined) {
    server.use(http.get('*/api/organization/members', () => HttpResponse.json(pagedMembers(members))))
  } else {
    server.use(http.get('*/api/organization/members', () => opts.memberResponse!))
  }
  server.use(http.get('*/api/organization/role-options', () => HttpResponse.json([])))

  const result = await renderComponent(MembersView, {
    initialState: {
      auth: {
        account: makeAccount(),
        organizationId: 'org-1',
        organizationName: 'Acme Inc',
        permissions: opts.permissions ?? [],
      },
    },
  })
  await flushPromises()
  return result
}

describe('MembersView', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    server.resetHandlers()
    vi.restoreAllMocks()
  })

  it('loads 50 members and appends the next page from Show more', async () => {
    const page1 = Array.from({ length: 50 }, (_, i) => makeMember({ accountId: `a${i}` }))
    const page2 = [makeMember({ accountId: 'a50', accountName: 'Extra' })]
    server.use(
      http.get('*/api/organization/members', ({ request }) => {
        const url = new URL(request.url)
        const page = Number(url.searchParams.get('pageNumber') ?? 1)
        if (page === 1) return HttpResponse.json(pagedMembers(page1, 51))
        return HttpResponse.json(pagedMembers(page2, 51, 2))
      }),
    )
    server.use(http.get('*/api/organization/role-options', () => HttpResponse.json([])))

    const { wrapper } = await renderComponent(MembersView, {
      initialState: { auth: { account: makeAccount(), permissions: [] } },
    })
    await flushPromises()

    // Show more button visible
    expect(wrapper.find('[data-testid="members-show-more"]').exists()).toBe(true)

    await wrapper.get('[data-testid="members-show-more"]').trigger('click')
    await flushPromises()

    // Now 51 members; show-more gone
    expect(wrapper.find('[data-testid="members-show-more"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid^="member-row-"]')).toHaveLength(51)
  })

  it('reloads the list after a 404 on change roles or remove', async () => {
    const member = makeMember({ accountId: 'a1', canRemove: true, isSelf: false })
    let listCalls = 0
    server.use(
      http.get('*/api/organization/members', () => {
        listCalls += 1
        return HttpResponse.json(pagedMembers([member]))
      }),
      http.delete('*/api/organization/members/*', () =>
        problem(404, { status: 404, title: 'Not found', code: 'ResourceNotFound' }),
      ),
    )
    server.use(http.get('*/api/organization/role-options', () => HttpResponse.json([])))

    const { wrapper } = await renderComponent(MembersView, {
      initialState: { auth: { account: makeAccount(), permissions: [] } },
    })
    await flushPromises()

    const initialListCalls = listCalls

    // Trigger remove via menu
    await wrapper.get('[data-testid="member-menu"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="member-remove"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="inline-confirm-confirm"]').trigger('click')
    await flushPromises()

    expect(listCalls).toBeGreaterThan(initialListCalls)
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(t('common.notFoundReloaded'))
  })

  it('shows the not-found state when the member list returns 404', async () => {
    const { wrapper } = await render({
      memberResponse: problem(404, { status: 404, title: 'Not found', code: 'ResourceNotFound' }),
    })

    expect(wrapper.text()).toContain(t('accessProblem.notFoundTitle'))
  })
})
