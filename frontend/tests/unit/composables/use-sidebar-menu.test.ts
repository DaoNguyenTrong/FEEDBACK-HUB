import type { RouteRecordRaw } from 'vue-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSidebarMenu } from '@/composables/use-sidebar-menu'
import { containsRoute, flattenMenuLinks, toNavSections } from '@/layouts/sidebar-menu'
import type { SidebarMenuEntry } from '@/layouts/sidebar-menu'
import type { MenuGroup } from '@/layouts/menu-groups'
import { MENU_GROUPS } from '@/layouts/menu-groups'
import { useAuthStore } from '@/stores/auth'
import { withSetup } from '../../helpers/with-setup'

const stub = { template: '<div />' }

function idOf(entry: SidebarMenuEntry) {
  return entry.kind === 'item' ? entry.routeName : entry.id
}

const adminGroup: MenuGroup = { id: 'admin', labelKey: 'nav.groups.admin', icon: stub, order: 2 }

function leaf(name: string, extra: Record<string, unknown> = {}): RouteRecordRaw {
  return {
    path: name,
    name,
    component: stub,
    meta: { sidebar: { labelKey: `nav.${name}`, icon: stub, ...extra } },
  }
}

function layoutRoutes(children: RouteRecordRaw[]): RouteRecordRaw[] {
  return [{ path: '/', name: 'layout', component: stub, children }]
}

describe('useSidebarMenu', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('hides an item whose route needs a permission the active organization does not grant', async () => {
    useAuthStore().permissions = ['reports.view']
    const routes = layoutRoutes([
      {
        path: 'reports',
        name: 'reports',
        component: stub,
        meta: { permission: 'reports.view', sidebar: { labelKey: 'nav.reports', icon: stub } },
      },
      {
        path: 'billing',
        name: 'billing',
        component: stub,
        meta: { permission: 'billing.manage', sidebar: { labelKey: 'nav.billing', icon: stub } },
      },
      {
        path: 'open',
        name: 'open',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.open', icon: stub } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map(idOf)).toEqual(['reports', 'open'])
  })

  it('shows a gated item once the permission arrives (e.g. after a token refresh)', async () => {
    const routes = layoutRoutes([
      {
        path: 'billing',
        name: 'billing',
        component: stub,
        meta: { permission: 'billing.manage', sidebar: { labelKey: 'nav.billing', icon: stub } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)
    expect(result.value).toHaveLength(0)

    useAuthStore().permissions = ['billing.manage']

    expect(result.value.map(idOf)).toEqual(['billing'])
  })

  it('excludes routes without meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'dashboard',
        name: 'dashboard',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.dashboard', icon: stub, order: 0 } },
      },
      { path: 'hidden', name: 'hidden', component: stub, meta: {} },
      { path: 'chat', name: 'chat', component: stub },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map(idOf)).toEqual(['dashboard'])
  })

  it('orders siblings by meta.sidebar.order ascending, defaulting missing order to 0', async () => {
    const routes = layoutRoutes([
      {
        path: 'third',
        name: 'third',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.third', icon: stub, order: 5 } },
      },
      {
        path: 'first',
        name: 'first',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.first', icon: stub, order: -1 } },
      },
      {
        path: 'second',
        name: 'second',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.second', icon: stub } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map(idOf)).toEqual(['first', 'second', 'third'])
  })

  it('builds a submenu item for a route whose children have meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'settings',
        name: 'settings',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.settings', icon: stub, order: 0 } },
        children: [
          {
            path: 'profile',
            name: 'settings-profile',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.profile', icon: stub, order: 0 } },
          },
          {
            path: 'security',
            name: 'settings-security',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.security', icon: stub, order: 1 } },
          },
        ],
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value).toHaveLength(1)
    const submenu = result.value[0] as Extract<SidebarMenuEntry, { kind: 'item' }>
    expect(submenu.routeName).toBeUndefined()
    expect(submenu.labelKey).toBe('nav.settings')
    expect(submenu.items?.map((item) => item.routeName)).toEqual([
      'settings-profile',
      'settings-security',
    ])
  })

  it('excludes a redirect route with no qualifying children even when it has meta.sidebar', async () => {
    const routes = layoutRoutes([
      {
        path: 'legacy',
        name: 'legacy',
        redirect: '/dashboard',
        meta: { sidebar: { labelKey: 'nav.legacy', icon: stub, order: 0 } },
      },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.dashboard', icon: stub, order: 1 } },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    expect(result.value.map(idOf)).toEqual(['dashboard'])
  })

  it('gathers routes that name the same group under one entry, ordered by item order', async () => {
    const routes = layoutRoutes([
      leaf('users', { group: 'admin', order: 2 }),
      leaf('roles', { group: 'admin', order: 1 }),
    ])

    const { result } = await withSetup(() => useSidebarMenu([adminGroup]), '/', routes)

    expect(result.value).toHaveLength(1)
    const group = result.value[0]!
    expect(group.kind).toBe('group')
    expect(group.kind === 'group' && group.items.map((item) => item.routeName)).toEqual([
      'roles',
      'users',
    ])
  })

  it('orders groups and ungrouped items together by order', async () => {
    const routes = layoutRoutes([
      leaf('c', { order: 3 }),
      leaf('users', { group: 'admin' }),
      leaf('a', { order: 1 }),
    ])

    const { result } = await withSetup(() => useSidebarMenu([adminGroup]), '/', routes)

    expect(result.value.map(idOf)).toEqual(['a', 'admin', 'c'])
  })

  it('drops a group left empty after the permission filter', async () => {
    const routes = layoutRoutes([
      {
        ...leaf('users', { group: 'admin' }),
        meta: {
          permission: 'users.manage',
          sidebar: { labelKey: 'nav.users', icon: stub, group: 'admin' },
        },
      },
    ])

    const { result } = await withSetup(() => useSidebarMenu([adminGroup]), '/', routes)

    expect(result.value).toEqual([])
  })

  it('treats an unknown group id as ungrouped and warns', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    const routes = layoutRoutes([leaf('users', { group: 'missing' })])

    const { result } = await withSetup(() => useSidebarMenu([adminGroup]), '/', routes)

    expect(result.value.map(idOf)).toEqual(['users'])
    expect(warn).toHaveBeenCalledTimes(1)
    warn.mockRestore()
  })

  it('keeps meta without group working as before', async () => {
    const routes = layoutRoutes([leaf('legacy', { order: 1 })])

    const { result } = await withSetup(() => useSidebarMenu([adminGroup]), '/', routes)

    expect(result.value.map(idOf)).toEqual(['legacy'])
    expect(result.value[0]!.kind).toBe('item')
  })
})

describe('useSidebarMenu access', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  function sections() {
    return layoutRoutes([
      leaf('home', { order: 0 }),
      {
        ...leaf('members'),
        meta: {
          requiresOrganization: true,
          permission: 'organizations.members.manage',
          sidebar: { labelKey: 'nav.members', icon: stub, group: 'organization' },
        },
      },
      {
        ...leaf('overview'),
        meta: {
          requiresOrganization: true,
          sidebar: { labelKey: 'nav.overview', icon: stub, group: 'organization', order: 1 },
        },
      },
      {
        ...leaf('tenants'),
        meta: {
          systemAdministrator: true,
          sidebar: { labelKey: 'nav.tenants', icon: stub, group: 'system' },
        },
      },
    ])
  }

  function groupItems(entry: SidebarMenuEntry) {
    return entry.kind === 'group' ? entry.items.map((item) => item.routeName) : []
  }

  it('shows the system group only to a system administrator', async () => {
    const auth = useAuthStore()
    auth.organizationId = 'org-1'
    const { result } = await withSetup(() => useSidebarMenu(), '/', sections())

    expect(result.value.map(idOf)).not.toContain('system')

    auth.isSystemAdministrator = true

    expect(result.value.map(idOf)).toContain('system')
  })

  it('hides a gated child under an allowed parent item', async () => {
    const auth = useAuthStore()
    const routes = layoutRoutes([
      {
        path: 'settings',
        name: 'settings',
        component: stub,
        meta: { sidebar: { labelKey: 'nav.settings', icon: stub } },
        children: [
          leaf('general'),
          {
            ...leaf('billing'),
            meta: {
              permission: 'billing.manage',
              sidebar: { labelKey: 'nav.billing', icon: stub },
            },
          },
        ],
      },
    ])
    const { result } = await withSetup(() => useSidebarMenu(), '/', routes)

    function childNames() {
      const parent = result.value[0]
      const items = parent?.kind === 'item' ? (parent as { items?: { routeName?: string }[] }).items : []
      return items?.map((item) => item.routeName)
    }

    expect(childNames()).toEqual(['general'])

    auth.permissions = ['billing.manage']

    expect(childNames()).toEqual(['general', 'billing'])
  })

  it('hides organization pages in the personal context', async () => {
    const auth = useAuthStore()
    auth.organizationId = null
    auth.permissions = ['organizations.members.manage']
    const { result } = await withSetup(() => useSidebarMenu(), '/', sections())

    expect(result.value.map(idOf)).toEqual(['home'])

    auth.organizationId = 'org-1'

    expect(result.value.map(idOf)).toEqual(['home', 'organization'])
  })

  it('hides a page whose permission the user lacks', async () => {
    const auth = useAuthStore()
    auth.organizationId = 'org-1'
    const { result } = await withSetup(() => useSidebarMenu(), '/', sections())

    expect(groupItems(result.value[1]!)).toEqual(['overview'])

    auth.permissions = ['organizations.members.manage']

    expect(groupItems(result.value[1]!)).toEqual(['members', 'overview'])
  })

  it('lists the home item, then the organization group, then the system group', async () => {
    const auth = useAuthStore()
    auth.organizationId = 'org-1'
    auth.isSystemAdministrator = true
    auth.permissions = ['organizations.members.manage']
    const { result } = await withSetup(() => useSidebarMenu(), '/', sections())

    expect(result.value.map(idOf)).toEqual(['home', 'organization', 'system'])
  })

  it('ships the organization and system groups as text headings', () => {
    expect(MENU_GROUPS.map((group) => [group.id, group.labelKey, group.order])).toEqual([
      ['organization', 'nav.groups.organization', 100],
      ['system', 'nav.groups.system', 200],
    ])
  })
})

describe('flattenMenuLinks', () => {
  it('returns leaves with their group and parent trail', () => {
    const entries: SidebarMenuEntry[] = [
      {
        kind: 'group',
        id: 'admin',
        labelKey: 'nav.groups.admin',
        icon: stub,
        items: [
          {
            labelKey: 'nav.reports',
            icon: stub,
            items: [{ labelKey: 'nav.monthly', icon: stub, routeName: 'monthly' }],
          },
        ],
      },
      { kind: 'item', labelKey: 'nav.home', icon: stub, routeName: 'home' },
    ]

    const links = flattenMenuLinks(entries)

    expect(links.map((link) => link.routeName)).toEqual(['monthly', 'home'])
    expect(links[0]!.trailKeys).toEqual(['nav.groups.admin', 'nav.reports'])
    expect(links[1]!.trailKeys).toEqual([])
  })
})

describe('containsRoute', () => {
  const group: SidebarMenuEntry = {
    kind: 'group',
    id: 'admin',
    labelKey: 'nav.groups.admin',
    icon: stub,
    items: [
      {
        labelKey: 'nav.reports',
        icon: stub,
        items: [{ labelKey: 'nav.monthly', icon: stub, routeName: 'monthly' }],
      },
    ],
  }

  it('is true for a group whose nested child matches', () => {
    expect(containsRoute(group, 'monthly')).toBe(true)
  })

  it('is false when no descendant matches', () => {
    expect(containsRoute(group, 'other')).toBe(false)
    expect(containsRoute(group, undefined)).toBe(false)
  })
})

describe('toNavSections', () => {
  const link = (routeName: string): SidebarMenuEntry => ({
    kind: 'item',
    labelKey: `nav.${routeName}`,
    icon: stub,
    routeName,
  })
  const group = (id: string, routeName: string): SidebarMenuEntry => ({
    kind: 'group',
    id,
    labelKey: `nav.groups.${id}`,
    icon: stub,
    items: [{ labelKey: `nav.${routeName}`, icon: stub, routeName }],
  })

  it('merges loose items that sit next to each other into one section', () => {
    const sections = toNavSections([link('home'), link('inbox'), group('admin', 'users')])

    expect(sections.map((section) => section.links.map((l) => l.routeName))).toEqual([
      ['home', 'inbox'],
      ['users'],
    ])
    expect(sections[0]!.headingKey).toBeUndefined()
    expect(sections[1]!.headingKey).toBe('nav.groups.admin')
  })

  it('gives every section a unique key when loose items follow a group', () => {
    const sections = toNavSections([link('home'), group('admin', 'users'), link('help')])

    expect(sections).toHaveLength(3)
    expect(new Set(sections.map((section) => section.key)).size).toBe(3)
  })
})
