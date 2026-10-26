import type { RouteMeta } from 'vue-router'

export type RouteAccess = 'allowed' | 'login' | 'no-organization' | 'forbidden'

export interface RouteAccessContext {
  isAuthenticated: boolean
  organizationId: string | null
  isSystemAdministrator: boolean
  hasPermission: (code: string) => boolean
}

// UI gate only: the API re-checks every request. Order matters — a missing organization is
// reported before a missing permission, because the permission set belongs to the active one.
export function routeAccess(meta: RouteMeta, ctx: RouteAccessContext): RouteAccess {
  if (meta.requiresAuth && !ctx.isAuthenticated) {
    return 'login'
  }

  if (meta.requiresOrganization && ctx.organizationId === null) {
    return 'no-organization'
  }

  if (meta.systemAdministrator && !ctx.isSystemAdministrator) {
    return 'forbidden'
  }

  if (meta.permission && !ctx.hasPermission(meta.permission)) {
    return 'forbidden'
  }

  return 'allowed'
}
