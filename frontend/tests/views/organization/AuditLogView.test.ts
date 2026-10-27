import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AuditLogView from '@/views/organization/AuditLogView.vue'
import i18n from '@/plugins/i18n'
import type { AuditEntryDto } from '@/api/types'
import { makeAuditEntry, makeRoleRef } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'

const t = i18n.global.t

// Fixtures sit between 05:00Z and 12:00Z, so each one falls on the same calendar day in any zone
// from UTC-5 to UTC+11: the test passes under TZ=UTC and in the machine's own zone.
const NOW = '2026-10-27T10:00:00Z'

function localParts(iso: string) {
  return Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })
      .formatToParts(new Date(iso))
      .map((part) => [part.type, part.value]),
  )
}

function localTime(iso: string) {
  const parts = localParts(iso)
  return `${parts.hour}:${parts.minute}`
}

function localDate(iso: string) {
  const parts = localParts(iso)
  return `${parts.day}/${parts.month}/${parts.year}`
}

const ENTRIES: AuditEntryDto[] = [
  makeAuditEntry({
    id: 5,
    occurredAt: '2026-10-27T09:40:00Z',
    type: 'OrganizationRenamed',
    parameters: { nameBefore: 'Acme', nameAfter: 'Acme Inc' },
  }),
  makeAuditEntry({
    id: 4,
    occurredAt: '2026-10-27T05:10:00Z',
    type: 'MemberLeft',
    parameters: { accountName: 'Bình', email: 'binh@acme.vn' },
  }),
  makeAuditEntry({
    id: 3,
    occurredAt: '2026-10-26T11:20:00Z',
    actorKind: 'SystemAdministrator',
    actorName: null,
    type: 'OrganizationReactivated',
    parameters: {},
  }),
  makeAuditEntry({
    id: 2,
    occurredAt: '2026-10-20T08:30:00Z',
    type: 'InvitationSent',
    parameters: { email: 'new@acme.vn', role: makeRoleRef() },
  }),
  makeAuditEntry({
    id: 1,
    occurredAt: '2026-10-20T06:00:00Z',
    type: 'OrganizationDeactivated',
    parameters: {},
  }),
]

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

async function render(permissions: string[] = ['auditlogs.view']) {
  const result = await renderComponent(AuditLogView, {
    initialState: {
      auth: {
        account: makeAccount(),
        organizationId: 'org-1',
        organizationName: 'Acme Inc',
        permissions,
      },
    },
  })
  await flushPromises()
  return result
}

function serve(items: AuditEntryDto[], nextCursor: string | null = null) {
  server.use(
    http.get('*/api/organization/audit-log', () => HttpResponse.json({ items, nextCursor })),
  )
}

describe('AuditLogView', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date(NOW) })
  })
  afterEach(() => {
    vi.useRealTimers()
    server.resetHandlers()
  })

  it('shows newest first, grouped by local day with the time of each entry', async () => {
    serve(ENTRIES)
    const { wrapper } = await render()

    expect(wrapper.get('h1').text()).toBe('Audit log')

    const headings = wrapper.findAll('[data-testid="audit-log-day"]').map((h) => h.text())
    expect(headings).toEqual([
      `Today · ${localDate('2026-10-27T09:40:00Z')}`,
      `Yesterday · ${localDate('2026-10-26T11:20:00Z')}`,
      localDate('2026-10-20T08:30:00Z'),
    ])

    const ids = wrapper
      .findAll('[data-testid^="audit-log-row-"]')
      .map((row) => row.attributes('data-testid'))
    expect(ids).toEqual([5, 4, 3, 2, 1].map((id) => `audit-log-row-${id}`))

    for (const entry of ENTRIES) {
      const time = wrapper.get(`[data-testid="audit-log-row-${entry.id}"] time`)
      expect(time.text()).toBe(localTime(entry.occurredAt))
      expect(time.attributes('datetime')).toBe(entry.occurredAt)
    }
  })

  it('shows the actor in bold, then the sentence, with initials or FH', async () => {
    serve(ENTRIES)
    const { wrapper } = await render()

    const renamed = wrapper.get('[data-testid="audit-log-row-5"]')
    expect(renamed.get('strong').text()).toBe('Nguyễn Văn A')
    expect(renamed.text()).toContain('renamed the organization: "Acme" → "Acme Inc"')
    expect(renamed.get('[data-testid="audit-log-avatar"]').text()).toBe('NA')

    const system = wrapper.get('[data-testid="audit-log-row-3"]')
    expect(system.get('strong').text()).toBe('FeedbackHub system administrator')
    expect(system.get('[data-testid="audit-log-avatar"]').text()).toBe('FH')
    expect(system.text()).toContain('reactivated the organization')
  })

  it('titles the page in Vietnamese', async () => {
    i18n.global.locale.value = 'vi'
    serve(ENTRIES)
    const { wrapper } = await render()

    expect(wrapper.get('h1').text()).toBe('Nhật ký kiểm toán')
    expect(wrapper.findAll('[data-testid="audit-log-day"]')[0]!.text()).toMatch(/^Hôm nay · /)
    expect(wrapper.findAll('[data-testid="audit-log-day"]')[1]!.text()).toMatch(/^Hôm qua · /)
  })

  it('appends older entries from Show more', async () => {
    const requested: (string | null)[] = []
    server.use(
      http.get('*/api/organization/audit-log', ({ request }) => {
        const cursor = new URL(request.url).searchParams.get('cursor')
        requested.push(cursor)
        return cursor === null
          ? HttpResponse.json({ items: ENTRIES.slice(0, 3), nextCursor: 'c1' })
          : HttpResponse.json({ items: ENTRIES.slice(2), nextCursor: null })
      }),
    )
    const { wrapper } = await render()
    expect(wrapper.findAll('[data-testid^="audit-log-row-"]')).toHaveLength(3)

    await wrapper.get('[data-testid="audit-log-show-more"]').trigger('click')
    await flushPromises()

    expect(requested).toEqual([null, 'c1'])
    expect(wrapper.findAll('[data-testid^="audit-log-row-"]')).toHaveLength(5)
    expect(wrapper.find('[data-testid="audit-log-show-more"]').exists()).toBe(false)
  })

  it('shows the empty state when there are no entries', async () => {
    serve([])
    const { wrapper } = await render()

    expect(wrapper.text()).toContain('No entries yet')
    expect(wrapper.find('[data-testid="audit-log-show-more"]').exists()).toBe(false)
  })

  it('keeps Show more when a page is empty but a cursor remains', async () => {
    serve([], 'c1')
    const { wrapper } = await render()

    expect(wrapper.text()).not.toContain('No entries yet')
    expect(wrapper.find('[data-testid="audit-log-show-more"]').exists()).toBe(true)
  })

  it('has no filter or search control and no IP or device text', async () => {
    serve(ENTRIES)
    const { wrapper } = await render()

    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.find('select').exists()).toBe(false)
    expect(wrapper.find('[role="searchbox"]').exists()).toBe(false)
    expect(wrapper.text()).not.toMatch(/\bIP\b|device|thiết bị|user agent/i)
  })

  it('shows the email in entries to a viewer without members.manage', async () => {
    serve(ENTRIES)
    const { wrapper } = await render(['auditlogs.view'])

    expect(wrapper.get('[data-testid="audit-log-row-4"]').text()).toContain('(binh@acme.vn)')
    expect(wrapper.get('[data-testid="audit-log-row-2"]').text()).toContain('new@acme.vn')
  })

  it('renders an actor name containing an img tag as text with no img element', async () => {
    const name = '<img src=x onerror=alert(1)>'
    serve([
      makeAuditEntry({
        id: 7,
        occurredAt: '2026-10-27T09:40:00Z',
        actorName: name,
        type: 'RoleDeleted',
        parameters: { role: { id: 'r', name, systemKind: null } },
      }),
    ])
    const { wrapper } = await render()

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.get('[data-testid="audit-log-row-7"] strong').text()).toBe(name)
    expect(wrapper.get('[data-testid="audit-log-row-7"]').text()).toContain(
      `deleted the role "${name}"`,
    )
  })

  it('uses the generic sentence for an unknown type', async () => {
    serve([
      makeAuditEntry({
        id: 8,
        occurredAt: '2026-10-27T09:40:00Z',
        type: 'FeedbackArchived' as AuditEntryDto['type'],
        parameters: { secret: 'do-not-print' },
      }),
    ])
    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="audit-log-row-8"]').text()).toContain(
      'performed an administrative action',
    )
    expect(wrapper.text()).not.toContain('do-not-print')
  })

  it('shows the no-permission state on 403 OrganizationAccessDenied', async () => {
    server.use(
      http.get('*/api/organization/audit-log', () =>
        problem(403, { status: 403, title: 'Forbidden', code: 'OrganizationAccessDenied' }),
      ),
    )
    const { wrapper } = await render()

    expect(wrapper.text()).toContain(t('accessProblem.forbiddenTitle'))
    expect(wrapper.find('[data-testid="audit-log-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid^="audit-log-row-"]').exists()).toBe(false)
  })

  it('offers Retry after a load failure', async () => {
    let calls = 0
    server.use(
      http.get('*/api/organization/audit-log', () => {
        calls += 1
        return calls === 1
          ? problem(500, { status: 500, title: 'Server error' })
          : HttpResponse.json({ items: ENTRIES, nextCursor: null })
      }),
    )
    const { wrapper } = await render()

    expect(wrapper.get('[data-testid="audit-log-error"]').text()).toContain(
      "Couldn't load the audit log.",
    )
    expect(wrapper.find('[data-testid^="audit-log-row-"]').exists()).toBe(false)

    await wrapper.get('[data-testid="audit-log-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="audit-log-error"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid^="audit-log-row-"]')).toHaveLength(5)
  })

  it('keeps the loaded entries and explains an InvalidCursor failure of Show more', async () => {
    server.use(
      http.get('*/api/organization/audit-log', ({ request }) =>
        new URL(request.url).searchParams.get('cursor') === null
          ? HttpResponse.json({ items: ENTRIES.slice(0, 2), nextCursor: 'bad' })
          : problem(400, { status: 400, title: 'Bad request', code: 'InvalidCursor' }),
      ),
    )
    const { wrapper } = await render()

    await wrapper.get('[data-testid="audit-log-show-more"]').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid^="audit-log-row-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="audit-log-more-error"]').text()).toContain(
      t('errorCodes.InvalidCursor'),
    )
  })
})
