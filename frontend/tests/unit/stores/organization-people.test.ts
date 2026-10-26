import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useOrganizationPeopleStore } from '@/stores/organization-people'
import { makeMember, makeRoleOption } from '../../factories/organization'
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
