import type { Component } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import type { RouteMeta } from 'vue-router'
import { BuildingCommunity, Home, Settings, Users } from '@vicons/tabler'
import { Permissions } from '@/lib/permissions'
import { routeAccess } from '@/lib/route-access'
import type { RouteAccessContext } from '@/lib/route-access'

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    guestOnly?: boolean
    /**
     * Permission code (`lib/permissions.ts`) the active organization must grant. Hides the route's
     * menu item; opened by URL without it, the layout shows "not enough permission" and keeps the
     * URL. A UI gate only — the API still checks every request. Never put one on `home`.
     */
    permission?: string
    /** The page lives inside the active organization: the personal context is sent to home. */
    requiresOrganization?: boolean
    /** A system administrator page. UI gate only; the API still checks `isSystemAdministrator`. */
    systemAdministrator?: boolean
    /** Route name of the menu item shown as open when this page has no item of its own. */
    activeMenu?: string
    sidebar?: {
      labelKey: string
      icon: Component
      order?: number
      /** `MenuGroup.id` from `layouts/menu-groups.ts`; omit for a standalone top-level item. */
      group?: string
    }
  }
}

const router = createRouter({
  history: createWebHashHistory(),
  // Only reaches pages that scroll the window (login, register…), where back/forward restores the
  // saved position. Inside the app shell the page scrolls `main` instead, which AppLayout resets to
  // the top on every path change — back/forward included; no position is saved for it.
  scrollBehavior: (_to, _from, savedPosition) => savedPosition ?? { top: 0 },
  routes: [
    {
      path: '/',
      component: () => import('@/layouts/AppLayout.vue'),
      children: [
        {
          path: '',
          name: 'home',
          component: () => import('@/views/HomeView.vue'),
          meta: {
            requiresAuth: true,
            sidebar: {
              labelKey: 'nav.home',
              icon: Home,
            },
          },
        },
        {
          path: 'organization/members',
          name: 'organization-members',
          component: () => import('@/views/organization/MembersView.vue'),
          meta: {
            requiresAuth: true,
            requiresOrganization: true,
            sidebar: {
              labelKey: 'nav.members',
              icon: Users,
              group: 'organization',
              order: 10,
            },
          },
        },
        {
          path: 'organization/members/invitations',
          name: 'organization-invitations',
          component: () => import('@/views/organization/InvitationsView.vue'),
          meta: {
            requiresAuth: true,
            requiresOrganization: true,
            permission: Permissions.OrganizationMembersManage,
            activeMenu: 'organization-members',
          },
        },
        {
          path: 'organization/members/roles',
          name: 'organization-roles',
          component: () => import('@/views/organization/RolesView.vue'),
          meta: {
            requiresAuth: true,
            requiresOrganization: true,
            permission: Permissions.OrganizationRolesManage,
            activeMenu: 'organization-members',
          },
        },
        {
          path: 'organization/settings',
          name: 'organization-settings',
          component: () => import('@/views/organization/OrganizationSettingsView.vue'),
          meta: {
            requiresAuth: true,
            requiresOrganization: true,
            permission: Permissions.OrganizationManage,
            sidebar: {
              labelKey: 'nav.organizationSettings',
              icon: Settings,
              group: 'organization',
              order: 20,
            },
          },
        },
        {
          // No `requiresOrganization`: a system administrator opens it from any context, even with an
          // empty `org_id`.
          path: 'system/organizations',
          name: 'system-organizations',
          component: () => import('@/views/system/SystemOrganizationsView.vue'),
          meta: {
            requiresAuth: true,
            systemAdministrator: true,
            sidebar: {
              labelKey: 'nav.systemOrganizations',
              icon: BuildingCommunity,
              group: 'system',
              order: 10,
            },
          },
        },
        {
          // Last child: an unknown path shows "not found" inside the shell, after sign-in if need be.
          path: '/:pathMatch(.*)*',
          name: 'not-found',
          component: () => import('@/views/NotFoundView.vue'),
          meta: { requiresAuth: true },
        },
      ],
    },
    {
      path: '/welcome',
      name: 'welcome',
      component: () => import('@/views/WelcomeView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/verify-email',
      name: 'verify-email',
      component: () => import('@/views/VerifyEmailView.vue'),
    },
    {
      // Public: the invitation link is opened from a mail, signed in or not. No `guestOnly`: a
      // signed-in invitee accepts here, and the page itself says what to do for the wrong account.
      path: '/invite',
      name: 'invite',
      component: () => import('@/views/InviteView.vue'),
    },
    {
      // Public like /verify-email: the link is opened from a mail, signed in or not.
      path: '/confirm-email-change',
      name: 'confirm-email-change',
      component: () => import('@/views/ConfirmEmailChangeView.vue'),
    },
    {
      // nginx sends a reader of the private user guide here when their session cookie is missing or
      // expired; the guard routes them through login, then the view sends them back to the guide.
      path: '/guide-auth',
      name: 'guide-auth',
      component: () => import('@/views/GuideAuthView.vue'),
      meta: { requiresAuth: true },
    },
  ],
})

export function resolveGuardRedirect(
  to: {
    meta: RouteMeta
    fullPath: string
    query: Record<string, unknown>
    name?: string | symbol | null
  },
  auth: RouteAccessContext,
  health: { isDown: boolean },
) {
  // During an outage the silent refresh couldn't run, so `isAuthenticated` is unreliable: don't
  // redirect (no /login flash); keep the URL so App.vue's recovery watcher can restore and route.
  if (health.isDown) {
    return undefined
  }

  const access = routeAccess(to.meta, auth)

  if (access === 'login') {
    // Preserve the destination so LoginView can return the user there after
    // signing in, instead of always dropping them on the home page.
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (access === 'no-organization') {
    return useHomeRoute()
  }

  // The rail-and-header shell has nothing to show without an organization; the picker is its home.
  if (to.name === 'home' && auth.isAuthenticated && auth.organizationId === null) {
    return { name: 'welcome' }
  }

  // 'forbidden' keeps the URL: AppLayout shows the "not enough permission" state in place.
  if (to.meta.guestOnly && auth.isAuthenticated) {
    return safeRedirectTarget(to.query.redirect) ?? useHomeRoute()
  }

  return undefined
}

router.beforeEach((to) => resolveGuardRedirect(to, useAuthStore(), useHealthStore()))

export default router
