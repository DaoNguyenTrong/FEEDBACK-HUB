import type { RouteMeta } from 'vue-router'
import { describe, expect, it } from 'vitest'
import { routeAccess } from '@/lib/route-access'
import type { RouteAccessContext } from '@/lib/route-access'

function context(overrides: Partial<RouteAccessContext> & { granted?: string[] } = {}) {
  const { granted = [], ...rest } = overrides

  return {
    isAuthenticated: true,
    organizationId: 'org-1',
    isSystemAdministrator: false,
    hasPermission: (code: string) => granted.includes(code),
    ...rest,
  } satisfies RouteAccessContext
}

describe('routeAccess', () => {
  it.each([
    [
      'login',
      'a signed-out user on a page that needs sign-in',
      { requiresAuth: true },
      { isAuthenticated: false },
    ],
    [
      'no-organization',
      'a user without an active organization on an organization page',
      { requiresAuth: true, requiresOrganization: true },
      { organizationId: null },
    ],
    [
      'forbidden',
      'a user who is not a system administrator on a system page',
      { requiresAuth: true, systemAdministrator: true },
      {},
    ],
    [
      'forbidden',
      'a user who lacks the page permission',
      { requiresAuth: true, permission: 'billing.manage' },
      { granted: ['reports.view'] },
    ],
    [
      'allowed',
      'a user who meets every condition',
      {
        requiresAuth: true,
        requiresOrganization: true,
        systemAdministrator: true,
        permission: 'billing.manage',
      },
      { isSystemAdministrator: true, granted: ['billing.manage'] },
    ],
    ['allowed', 'a signed-in user on a page with no conditions', { requiresAuth: true }, {}],
    ['allowed', 'a signed-out user on a public page', {}, { isAuthenticated: false }],
  ] as [string, string, RouteMeta, Partial<RouteAccessContext> & { granted?: string[] }][])(
    'returns %s for %s',
    (expected, _label, meta, overrides) => {
      expect(routeAccess(meta, context(overrides))).toBe(expected)
    },
  )

  it('checks sign-in before the organization', () => {
    const meta: RouteMeta = { requiresAuth: true, requiresOrganization: true }

    expect(routeAccess(meta, context({ isAuthenticated: false, organizationId: null }))).toBe(
      'login',
    )
  })

  it('checks the organization before the permission', () => {
    const meta: RouteMeta = { requiresAuth: true, requiresOrganization: true, permission: 'x.y' }

    expect(routeAccess(meta, context({ organizationId: null }))).toBe('no-organization')
  })

  it('checks the organization before the system administrator flag', () => {
    const meta: RouteMeta = { requiresOrganization: true, systemAdministrator: true }

    expect(routeAccess(meta, context({ organizationId: null }))).toBe('no-organization')
  })

  it('refuses a permission-gated page to a signed-out user when the page does not require sign-in', () => {
    expect(routeAccess({ permission: 'x.y' }, context({ isAuthenticated: false }))).toBe(
      'forbidden',
    )
  })

  it('refuses a system page to a signed-out user when the page does not require sign-in', () => {
    expect(routeAccess({ systemAdministrator: true }, context({ isAuthenticated: false }))).toBe(
      'forbidden',
    )
  })
})
