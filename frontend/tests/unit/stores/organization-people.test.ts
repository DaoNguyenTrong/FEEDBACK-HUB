import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import {
  makeInvitation,
  makeMember,
  makePermission,
  makeRole,
  makeRoleOption,
} from '../../factories/organization'
import { makeAccount } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { setupTestPinia, storeIn } from '../../helpers/pinia'
import { Permissions } from '@/lib/permissions'

function pagedMembers(
  items: ReturnType<typeof makeMember>[],
  total = items.length,
  page = 1,
  pageSize = 50,
) {
  return { items, totalCount: total, pageNumber: page, pageSize, totalPages: 1 }
}

describe('organization-people store — members', () => {
  let pinia: ReturnType<typeof setupTestPinia>

  beforeEach(() => {
    pinia = setupTestPinia()
  })
  afterEach(() => {
    server.resetHandlers()
  })

  it('loads the first 50 members and exposes them on members.items', async () => {
    const members = [makeMember(), makeMember({ accountId: 'account-3', accountName: 'Bob' })]
    server.use(http.get('*/api/organization/members', () => HttpResponse.json(pagedMembers(members))))

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.members.loadMore()

    expect(store.members.items).toHaveLength(2)
    expect(store.members.totalCount).toBe(2)
  })

  it('appends the second page via loadMore', async () => {
    const page1 = [makeMember({ accountId: 'a1', accountName: 'A' })]
    const page2 = [makeMember({ accountId: 'a2', accountName: 'B' })]
    server.use(
      http.get('*/api/organization/members', ({ request }) => {
        const url = new URL(request.url)
        const page = Number(url.searchParams.get('pageNumber') ?? 1)
        const items = page === 1 ? page1 : page2
        return HttpResponse.json(pagedMembers(items, 2, page))
      }),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.members.loadMore()
    await store.members.loadMore()

    expect(store.members.items).toHaveLength(2)
  })

  it('reloads the list from page 1 via members.reload()', async () => {
    const member = makeMember()
    server.use(http.get('*/api/organization/members', () => HttpResponse.json(pagedMembers([member]))))

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.members.loadMore()
    expect(store.members.items).toHaveLength(1)

    await store.members.reload()
    expect(store.members.items).toHaveLength(1)
  })
})

describe('organization-people store — roleOptions', () => {
  let pinia: ReturnType<typeof setupTestPinia>

  beforeEach(() => {
    pinia = setupTestPinia()
  })
  afterEach(() => {
    server.resetHandlers()
  })

  it('loads role options when the caller has members.manage', async () => {
    const options = [makeRoleOption()]
    server.use(http.get('*/api/organization/role-options', () => HttpResponse.json(options)))
    pinia.state.value['auth'] = {
      account: makeAccount(),
      permissions: [Permissions.OrganizationMembersManage],
    }

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadRoleOptions()

    expect(store.roleOptions).toHaveLength(1)
  })

  it('skips loading role options without members.manage', async () => {
    let calls = 0
    server.use(http.get('*/api/organization/role-options', () => { calls += 1; return HttpResponse.json([]) }))
    pinia.state.value['auth'] = { account: makeAccount(), permissions: [] }

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadRoleOptions()

    expect(calls).toBe(0)
    expect(store.roleOptions).toHaveLength(0)
  })
})

describe('organization-people store — changeRoles / removeMember / leave', () => {
  let pinia: ReturnType<typeof setupTestPinia>

  beforeEach(() => {
    pinia = setupTestPinia()
  })
  afterEach(() => {
    server.resetHandlers()
  })

  it('updates the member in the list after changeRoles', async () => {
    const original = makeRoleOption({ id: 'role-admin', name: 'Admin', systemKind: 'Admin', canGrant: true })
    const member = makeMember({ accountId: 'a1', roleNames: ['Member'] })
    server.use(
      http.get('*/api/organization/members', () => HttpResponse.json(pagedMembers([member]))),
      http.get('*/api/organization/role-options', () => HttpResponse.json([original])),
      http.put('*/api/organization/members/*/roles', () => new HttpResponse(null, { status: 204 })),
    )
    pinia.state.value['auth'] = {
      account: makeAccount(),
      permissions: [Permissions.OrganizationMembersManage],
    }

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.members.loadMore()
    await store.loadRoleOptions()
    await store.changeRoles('a1', ['role-admin'])

    expect(store.members.items[0]!.roleNames).toEqual(['Admin'])
  })

  it('removes the member from the list after removeMember', async () => {
    const member = makeMember({ accountId: 'a1' })
    server.use(
      http.get('*/api/organization/members', () => HttpResponse.json(pagedMembers([member], 1))),
      http.delete('*/api/organization/members/*', () => new HttpResponse(null, { status: 204 })),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.members.loadMore()
    await store.removeMember('a1')

    expect(store.members.items).toHaveLength(0)
    expect(store.members.totalCount).toBe(0)
  })

  it('calls leave and resolves without modifying the list', async () => {
    server.use(http.post('*/api/organization/leave', () => new HttpResponse(null, { status: 204 })))

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await expect(store.leave()).resolves.toBeUndefined()
  })
})

describe('organization-people store — invitations', () => {
  let pinia: ReturnType<typeof setupTestPinia>

  beforeEach(() => {
    pinia = setupTestPinia()
  })
  afterEach(() => {
    server.resetHandlers()
  })

  it('loads invitations via invitations.loadMore', async () => {
    const inv = makeInvitation()
    server.use(
      http.get('*/api/organization/invitations', () =>
        HttpResponse.json({ items: [inv], totalCount: 1, pageNumber: 1, pageSize: 50, totalPages: 1 }),
      ),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.invitations.loadMore()

    expect(store.invitations.items).toHaveLength(1)
    expect(store.invitations.totalCount).toBe(1)
  })

  it('adds a new invitation to the top and removes an old one with the same email', async () => {
    const old = makeInvitation({ id: 'inv-old', email: 'dup@example.com' })
    const fresh = makeInvitation({ id: 'inv-new', email: 'dup@example.com' })
    server.use(
      http.get('*/api/organization/invitations', () =>
        HttpResponse.json({ items: [old], totalCount: 1, pageNumber: 1, pageSize: 50, totalPages: 1 }),
      ),
      http.post('*/api/organization/invitations', () => HttpResponse.json(fresh, { status: 201 })),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.invitations.loadMore()
    await store.invite('dup@example.com', 'role-member')

    expect(store.invitations.items[0]!.id).toBe('inv-new')
    expect(store.invitations.items.filter((i) => i.email === 'dup@example.com')).toHaveLength(1)
  })

  it('replaces the row with the new invitation after resend', async () => {
    const old = makeInvitation({ id: 'inv-1' })
    const updated = makeInvitation({ id: 'inv-2', email: old.email })
    server.use(
      http.get('*/api/organization/invitations', () =>
        HttpResponse.json({ items: [old], totalCount: 1, pageNumber: 1, pageSize: 50, totalPages: 1 }),
      ),
      http.post('*/api/organization/invitations/inv-1/resend', () => HttpResponse.json(updated)),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.invitations.loadMore()
    await store.resend('inv-1')

    expect(store.invitations.items[0]!.id).toBe('inv-2')
    expect(store.invitations.items).toHaveLength(1)
  })

  it('removes the invitation from the list after revokeOrDelete', async () => {
    const inv = makeInvitation({ id: 'inv-1' })
    server.use(
      http.get('*/api/organization/invitations', () =>
        HttpResponse.json({ items: [inv], totalCount: 1, pageNumber: 1, pageSize: 50, totalPages: 1 }),
      ),
      http.delete('*/api/organization/invitations/*', () => new HttpResponse(null, { status: 204 })),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.invitations.loadMore()
    await store.revokeOrDelete('inv-1')

    expect(store.invitations.items).toHaveLength(0)
  })
})

describe('organization-people store — roles', () => {
  let pinia: ReturnType<typeof setupTestPinia>

  beforeEach(() => {
    pinia = setupTestPinia()
  })
  afterEach(() => {
    server.resetHandlers()
  })

  it('loads the roles into roles', async () => {
    server.use(
      http.get('*/api/organization/roles', () =>
        HttpResponse.json([makeRole({ id: 'r1' }), makeRole({ id: 'r2' })]),
      ),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadRoles()

    expect(store.roles.map((r) => r.id)).toEqual(['r1', 'r2'])
  })

  it('loads the permission catalog once', async () => {
    let calls = 0
    server.use(
      http.get('*/api/permissions', () => {
        calls += 1
        return HttpResponse.json([makePermission(Permissions.FilesManage)])
      }),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadPermissionCatalog()
    await store.loadPermissionCatalog()

    expect(calls).toBe(1)
    expect(store.permissionCatalog).toEqual([{ code: Permissions.FilesManage }])
  })

  it('sends the name and permission codes on createRole and adds the role to the list', async () => {
    let body: unknown
    server.use(
      http.post('*/api/organization/roles', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(makeRole({ id: 'new', name: 'Support' }))
      }),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.createRole('Support', [Permissions.FilesManage])

    expect(body).toEqual({ name: 'Support', permissionCodes: [Permissions.FilesManage] })
    expect(store.roles.map((r) => r.id)).toEqual(['new'])
  })

  it('replaces the role in the list on updateRole', async () => {
    server.use(
      http.get('*/api/organization/roles', () =>
        HttpResponse.json([makeRole({ id: 'r1', name: 'Old' })]),
      ),
      http.put('*/api/organization/roles/r1', () =>
        HttpResponse.json(makeRole({ id: 'r1', name: 'New' })),
      ),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadRoles()
    await store.updateRole('r1', 'New', [])

    expect(store.roles.map((r) => r.name)).toEqual(['New'])
  })

  it('removes the role from the list on deleteRole', async () => {
    server.use(
      http.get('*/api/organization/roles', () => HttpResponse.json([makeRole({ id: 'r1' })])),
      http.delete('*/api/organization/roles/r1', () => new HttpResponse(null, { status: 204 })),
    )

    const store = storeIn(pinia, useOrganizationPeopleStore)
    await store.loadRoles()
    await store.deleteRole('r1')

    expect(store.roles).toHaveLength(0)
  })
})
