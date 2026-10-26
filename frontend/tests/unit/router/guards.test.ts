import type { RouteMeta, RouteRecordRaw } from 'vue-router'
import { describe, expect, it } from 'vitest'
import router, { resolveGuardRedirect } from '@/router'
import { routeAccess } from '@/lib/route-access'
import type { RouteAccessContext } from '@/lib/route-access'

function route(
  meta: RouteMeta,
  extra: { fullPath?: string; query?: Record<string, unknown>; name?: string } = {},
) {
  return { meta, fullPath: extra.fullPath ?? '/target', query: extra.query ?? {}, name: extra.name }
}

const online = { isDown: false }

function signedIn(permissions: string[] = [], overrides: Partial<RouteAccessContext> = {}) {
  return {
    isAuthenticated: true,
    organizationId: 'org-1',
    isSystemAdministrator: false,
    hasPermission: (code: string) => permissions.includes(code),
    ...overrides,
  } satisfies RouteAccessContext
}

const signedOut: RouteAccessContext = {
  isAuthenticated: false,
  organizationId: null,
  isSystemAdministrator: false,
  hasPermission: () => false,
}

describe('resolveGuardRedirect', () => {
  // The URL stays: AppLayout shows the "not enough permission" state in place of the page.
  it('keeps the URL for a signed-in user without the page permission', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }),
      signedIn(['reports.view']),
      online,
    )

    expect(result).toBeUndefined()
  })

  it('keeps the URL for a non-administrator on a system page', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, systemAdministrator: true }),
      signedIn(),
      online,
    )

    expect(result).toBeUndefined()
  })

  it('sends a user without an active organization to the home route on an organization page', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, requiresOrganization: true }),
      signedIn([], { organizationId: null }),
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('sends a signed-in user without an organization from home to welcome', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { name: 'home', fullPath: '/' }),
      signedIn([], { organizationId: null }),
      online,
    )

    expect(result).toEqual({ name: 'welcome' })
  })

  it('keeps a user in an organization on home', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { name: 'home', fullPath: '/' }),
      signedIn(),
      online,
    )

    expect(result).toBeUndefined()
  })

  it('keeps a user without an organization on welcome', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { name: 'welcome', fullPath: '/welcome' }),
      signedIn([], { organizationId: null }),
      online,
    )

    expect(result).toBeUndefined()
  })

  it('lets a user in an organization through an organization page', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, requiresOrganization: true }),
      signedIn(),
      online,
    )

    expect(result).toBeUndefined()
  })

  it('does not gate anything during an API outage', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, requiresOrganization: true, systemAdministrator: true }),
      signedOut,
      { isDown: true },
    )

    expect(result).toBeUndefined()
  })

  it('lets a signed-in user with the route permission through', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }),
      signedIn(['billing.manage']),
      online,
    )

    expect(result).toBeUndefined()
  })

  // Login comes first: the permission is only knowable once there is a session.
  it('sends a signed-out user on a gated route to login, keeping the destination', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true, permission: 'billing.manage' }, { fullPath: '/billing' }),
      signedOut,
      online,
    )

    expect(result).toEqual({ name: 'login', query: { redirect: '/billing' } })
  })

  it('redirects to login (keeping the destination) when requiresAuth and not authenticated', () => {
    const result = resolveGuardRedirect(
      route({ requiresAuth: true }, { fullPath: '/organizations' }),
      signedOut,
      online,
    )

    expect(result).toEqual({ name: 'login', query: { redirect: '/organizations' } })
  })

  it('redirects to home when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedIn(), online)

    expect(result).toEqual({ name: 'home' })
  })

  it('follows a safe redirect query when guestOnly and already authenticated', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '/organizations' } }),
      signedIn(),
      online,
    )

    expect(result).toBe('/organizations')
  })

  it('ignores an off-origin redirect query and falls back to home', () => {
    const result = resolveGuardRedirect(
      route({ guestOnly: true }, { query: { redirect: '//evil.example/phish' } }),
      signedIn(),
      online,
    )

    expect(result).toEqual({ name: 'home' })
  })

  it('allows access to a requiresAuth route when authenticated', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), signedIn(), online)

    expect(result).toBeUndefined()
  })

  it('allows access to a guestOnly route when not authenticated', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedOut, online)

    expect(result).toBeUndefined()
  })

  it('allows access to a route with neither meta flag regardless of auth state', () => {
    expect(resolveGuardRedirect(route({}), signedOut, online)).toBeUndefined()
    expect(resolveGuardRedirect(route({}), signedIn(), online)).toBeUndefined()
  })

  it('does not redirect to login during an API outage even on a requiresAuth route', () => {
    const result = resolveGuardRedirect(route({ requiresAuth: true }), signedOut, { isDown: true })

    expect(result).toBeUndefined()
  })

  it('does not redirect away from a guestOnly route during an API outage', () => {
    const result = resolveGuardRedirect(route({ guestOnly: true }), signedIn(), { isDown: true })

    expect(result).toBeUndefined()
  })
})

describe('routes', () => {
  // nginx sends a reader without a session here; the guard must route them through login first.
  it('requires a session for the guide hand-off route', () => {
    expect(router.resolve('/guide-auth').meta.requiresAuth).toBe(true)
  })
})

describe('route table', () => {
  function walk(routes: readonly RouteRecordRaw[]): RouteRecordRaw[] {
    return routes.flatMap((record) => [record, ...walk(record.children ?? [])])
  }

  // home is the fallback every redirect lands on: gating it would loop or strand the user.
  it('allows every route that needs only a session to a signed-in user with no permissions', () => {
    const records = walk(router.options.routes).filter(
      (record) =>
        record.meta?.requiresAuth &&
        !record.meta.permission &&
        !record.meta.requiresOrganization &&
        !record.meta.systemAdministrator,
    )

    expect(records.map((record) => record.name)).toContain('home')
    for (const record of records) {
      expect(routeAccess(record.meta ?? {}, signedIn([], { organizationId: null }))).toBe('allowed')
    }
  })

  it('never puts a permission, organization or system gate on home', () => {
    const home = router.resolve({ name: 'home' })

    expect(home.meta.permission).toBeUndefined()
    expect(home.meta.requiresOrganization).toBeUndefined()
    expect(home.meta.systemAdministrator).toBeUndefined()
  })
})
