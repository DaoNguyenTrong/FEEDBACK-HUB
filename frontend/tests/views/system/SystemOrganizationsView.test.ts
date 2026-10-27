import { DOMWrapper, flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import SystemOrganizationsView from '@/views/system/SystemOrganizationsView.vue'
import router from '@/router'
import { buildMenuEntries } from '@/composables/use-sidebar-menu'
import { MENU_GROUPS } from '@/layouts/menu-groups'
import { routeAccess } from '@/lib/route-access'
import * as feedback from '@/lib/feedback'
import i18n from '@/plugins/i18n'
import { makeSystemOrganization } from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { renderComponent } from '../../helpers/render'
import { stubViewport } from '../../helpers/viewport'

const t = i18n.global.t

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function paged(items: ReturnType<typeof makeSystemOrganization>[], total = items.length, page = 1) {
  return {
    items,
    totalCount: total,
    pageNumber: page,
    pageSize: 50,
    totalPages: Math.ceil(total / 50),
  }
}

async function render(width = 1280) {
  stubViewport(width)
  const result = await renderComponent(SystemOrganizationsView, {
    attachTo: document.body,
    global: { stubs: { teleport: true } },
    initialState: {
      auth: { account: makeAccount(), organizationId: 'org-0', isSystemAdministrator: true },
    },
  })
  await flushPromises()
  return result
}

const find = (testid: string) =>
  document.body.querySelector<HTMLElement>(`[data-testid="${testid}"]`)
const findAll = (prefix: string) =>
  Array.from(document.body.querySelectorAll<HTMLElement>(`[data-testid^="${prefix}"]`))

async function press(testid: string) {
  await new DOMWrapper(find(testid)!).trigger('click')
  await flushPromises()
}

describe('SystemOrganizationsView', () => {
  beforeEach(() => {
    vi.spyOn(feedback, 'showSuccessMessage').mockImplementation(() => {})
    vi.spyOn(feedback, 'showErrorMessage').mockImplementation(() => {})
  })
  afterEach(() => {
    document.body.innerHTML = ''
    server.resetHandlers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('lists organizations with member counts and the deactivated label in server order', async () => {
    server.use(
      http.get('*/api/system/organizations', () =>
        HttpResponse.json(
          paged([
            makeSystemOrganization({ id: 'o-z', name: 'Zeta Corp', memberCount: 7 }),
            makeSystemOrganization({
              id: 'o-a',
              name: 'Alpha Ltd',
              memberCount: 1,
              isDeactivated: true,
            }),
          ]),
        ),
      ),
    )
    await render()

    expect(document.body.textContent).toContain(t('systemOrganizations.count', { n: 2 }))
    const rows = findAll('system-org-row-')
    expect(rows.map((row) => row.dataset.testid)).toEqual([
      'system-org-row-o-z',
      'system-org-row-o-a',
    ])
    expect(rows[0]!.textContent).toContain('Zeta Corp')
    expect(rows[0]!.textContent).toContain(t('systemOrganizations.members', { n: 7 }))
    expect(rows[0]!.textContent).not.toContain(t('systemOrganizations.deactivated'))
    expect(rows[1]!.textContent).toContain(t('systemOrganizations.deactivated'))
    expect(find('system-org-deactivate-o-z')).not.toBeNull()
    expect(find('system-org-reactivate-o-a')).not.toBeNull()
  })

  it('appends the next page from Show more', async () => {
    const page1 = Array.from({ length: 50 }, (_, i) => makeSystemOrganization({ id: `o${i}` }))
    const page2 = [makeSystemOrganization({ id: 'o50', name: 'Extra' })]
    server.use(
      http.get('*/api/system/organizations', ({ request }) => {
        const page = Number(new URL(request.url).searchParams.get('pageNumber') ?? 1)
        return HttpResponse.json(page === 1 ? paged(page1, 51) : paged(page2, 51, 2))
      }),
    )
    await render()

    expect(findAll('system-org-row-')).toHaveLength(50)
    expect(document.body.textContent).toContain(
      t('systemOrganizations.showingOf', { shown: 50, total: 51 }),
    )

    await press('system-orgs-show-more')

    expect(findAll('system-org-row-')).toHaveLength(51)
    expect(find('system-orgs-show-more')).toBeNull()
  })

  it('requires a reason before deactivating and marks the row after 204', async () => {
    let body: unknown = null
    server.use(
      http.get('*/api/system/organizations', () =>
        HttpResponse.json(paged([makeSystemOrganization({ id: 'o1', name: 'Acme Inc' })])),
      ),
      http.post('*/api/system/organizations/o1/deactivate', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await render()

    await press('system-org-deactivate-o1')
    expect(find('deactivate-org-dialog')).not.toBeNull()
    expect(document.body.textContent).toContain(
      t('deactivateOrganization.title', { name: 'Acme Inc' }),
    )
    expect(document.body.textContent).not.toContain(t('deactivateOrganization.optional'))

    await press('deactivate-org-submit')
    expect(body).toBeNull()
    expect(find('deactivate-org-reason-error')?.textContent).toContain(
      t('errorCodes.DeactivationReasonRequired'),
    )

    const box = new DOMWrapper(find('deactivate-org-reason') as HTMLTextAreaElement)
    await box.setValue('Abuse report')
    await press('deactivate-org-submit')

    expect(body).toEqual({ reason: 'Abuse report' })
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('systemOrganizations.deactivatedToast', { name: 'Acme Inc' }),
    )
    const row = find('system-org-row-o1')!
    expect(row.textContent).toContain(t('systemOrganizations.deactivated'))
    expect(find('system-org-reactivate-o1')).not.toBeNull()
    expect(find('system-org-deactivate-o1')).toBeNull()
  })

  it('does not show the owner email hint under the reason field', async () => {
    server.use(
      http.get('*/api/system/organizations', () =>
        HttpResponse.json(paged([makeSystemOrganization({ id: 'o1' })])),
      ),
    )
    await render()

    await press('system-org-deactivate-o1')

    const dialog = find('deactivate-org-dialog')!
    expect(dialog.textContent).not.toMatch(/e-?mail/i)
    expect(dialog.textContent).toContain(t('deactivateOrganization.system.data'))
  })

  it('confirms reactivation below the row and clears the label after 204', async () => {
    let called = false
    server.use(
      http.get('*/api/system/organizations', () =>
        HttpResponse.json(
          paged([makeSystemOrganization({ id: 'o1', name: 'Acme Inc', isDeactivated: true })]),
        ),
      ),
      http.post('*/api/system/organizations/o1/reactivate', () => {
        called = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await render()

    await press('system-org-reactivate-o1')
    expect(called).toBe(false)
    const row = find('system-org-row-o1')!
    expect(row.querySelector('[data-testid="inline-confirm-confirm"]')).not.toBeNull()
    expect(row.textContent).toContain(
      t('systemOrganizations.reactivateConfirm', { name: 'Acme Inc' }),
    )
    expect(row.textContent).toContain(t('systemOrganizations.reactivateDetail'))

    await press('inline-confirm-confirm')

    expect(called).toBe(true)
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(
      t('systemOrganizations.reactivatedToast', { name: 'Acme Inc' }),
    )
    expect(find('system-org-row-o1')!.textContent).not.toContain(
      t('systemOrganizations.deactivated'),
    )
    expect(find('inline-confirm-confirm')).toBeNull()
    expect(find('system-org-deactivate-o1')).not.toBeNull()
  })

  it('reloads the list and says so when the organization no longer exists', async () => {
    let listCalls = 0
    server.use(
      http.get('*/api/system/organizations', () => {
        listCalls += 1
        return HttpResponse.json(paged([makeSystemOrganization({ id: 'o1', isDeactivated: true })]))
      }),
      http.post('*/api/system/organizations/o1/reactivate', () =>
        problem(404, { status: 404, title: 'Not found', code: 'ResourceNotFound' }),
      ),
    )
    await render()

    await press('system-org-reactivate-o1')
    await press('inline-confirm-confirm')

    expect(listCalls).toBe(2)
    expect(feedback.showSuccessMessage).toHaveBeenCalledWith(t('common.notFoundReloaded'))
  })

  it('opens the row actions as a bottom sheet at 390 px', async () => {
    let body: unknown = null
    server.use(
      http.get('*/api/system/organizations', () =>
        HttpResponse.json(
          paged([
            makeSystemOrganization({ id: 'o1', name: 'Acme Inc', memberCount: 4 }),
            makeSystemOrganization({ id: 'o2', name: 'Beta', isDeactivated: true }),
          ]),
        ),
      ),
      http.post('*/api/system/organizations/o1/deactivate', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await render(390)

    expect(find('system-org-deactivate-o1')).toBeNull()
    expect(find('app-sheet')).toBeNull()

    await press('system-org-actions-o1')

    const sheet = find('app-sheet')!
    expect(sheet.textContent).toContain('Acme Inc')
    expect(sheet.textContent).toContain(t('systemOrganizations.members', { n: 4 }))

    await press('system-org-sheet-deactivate')
    expect(find('deactivate-org-dialog')).not.toBeNull()

    const box = new DOMWrapper(find('deactivate-org-reason') as HTMLTextAreaElement)
    await box.setValue('Spam')
    await press('deactivate-org-submit')

    expect(body).toEqual({ reason: 'Spam' })
    expect(find('system-org-row-o1')!.textContent).toContain(t('systemOrganizations.deactivated'))
  })

  it('shows the no-permission state on 403 SystemAdministratorRequired', async () => {
    server.use(
      http.get('*/api/system/organizations', () =>
        problem(403, {
          status: 403,
          title: 'Forbidden',
          code: 'SystemAdministratorRequired',
          detail: 'Secret detail',
        }),
      ),
    )
    await render()

    expect(document.body.textContent).toContain(t('accessProblem.forbiddenTitle'))
    expect(document.body.textContent).not.toContain('Secret detail')
    expect(findAll('system-org-row-')).toHaveLength(0)
  })
})

describe('system-organizations route', () => {
  const route = router.resolve('/system/organizations')
  const administrator = {
    isAuthenticated: true,
    organizationId: null,
    isSystemAdministrator: true,
    hasPermission: () => false,
  }

  it('is gated by the system administrator flag only', () => {
    expect(route.name).toBe('system-organizations')
    expect(route.meta).toMatchObject({ requiresAuth: true, systemAdministrator: true })
    expect(route.meta.requiresOrganization).toBeUndefined()
  })

  it('is hidden from the rail and forbidden for a non-administrator', () => {
    const layout = router.options.routes.find((r) => r.path === '/')!
    const user = { ...administrator, organizationId: 'org-1', isSystemAdministrator: false }

    expect(routeAccess(route.meta, user)).toBe('forbidden')
    const ids = buildMenuEntries(layout.children ?? [], user, MENU_GROUPS).map((entry) =>
      entry.kind === 'group' ? entry.id : entry.routeName,
    )
    expect(ids).not.toContain('system')
  })

  it('opens for a system administrator whose token has an empty org_id and the rail shows only system items', () => {
    const layout = router.options.routes.find((r) => r.path === '/')!

    expect(routeAccess(route.meta, administrator)).toBe('allowed')
    const entries = buildMenuEntries(layout.children ?? [], administrator, MENU_GROUPS)
    const system = entries.find((entry) => entry.kind === 'group' && entry.id === 'system')

    expect(system?.kind === 'group' && system.items.map((item) => item.routeName)).toEqual([
      'system-organizations',
    ])
    expect(entries.filter((entry) => entry.kind === 'group').map((entry) => entry.id)).toEqual([
      'system',
    ])
  })
})
