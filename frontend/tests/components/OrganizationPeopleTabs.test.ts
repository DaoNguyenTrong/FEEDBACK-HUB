import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import OrganizationPeopleTabs from '@/components/OrganizationPeopleTabs.vue'
import i18n from '@/plugins/i18n'
import { Permissions } from '@/lib/permissions'
import { makeAccount } from '../helpers/msw/fixtures'
import { renderComponent } from '../helpers/render'

const t = i18n.global.t

async function render(permissions: string[] = [], active: 'members' | 'invitations' | 'roles' = 'members') {
  const result = await renderComponent(OrganizationPeopleTabs, {
    props: { active },
    initialState: { auth: { account: makeAccount(), permissions } },
  })
  await flushPromises()
  return result
}

describe('OrganizationPeopleTabs', () => {
  it('shows only the members tab to a member without management permissions', async () => {
    const { wrapper } = await render([])

    expect(wrapper.find('[data-testid="tab-members"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-invitations"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="tab-roles"]').exists()).toBe(false)
  })

  it('shows the invitations tab with members.manage and the roles tab with roles.manage', async () => {
    const { wrapper } = await render([
      Permissions.OrganizationMembersManage,
      Permissions.OrganizationRolesManage,
    ])

    expect(wrapper.find('[data-testid="tab-members"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-invitations"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-roles"]').exists()).toBe(true)
  })

  it('shows invitations tab with members.manage even without roles.manage', async () => {
    const { wrapper } = await render([Permissions.OrganizationMembersManage])

    expect(wrapper.find('[data-testid="tab-invitations"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-roles"]').exists()).toBe(false)
  })

  it('shows roles tab with roles.manage even without members.manage', async () => {
    const { wrapper } = await render([Permissions.OrganizationRolesManage])

    expect(wrapper.find('[data-testid="tab-roles"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-invitations"]').exists()).toBe(false)
  })

  it('renders the action slot', async () => {
    const result = await renderComponent(OrganizationPeopleTabs, {
      props: { active: 'members' },
      slots: { action: '<button data-testid="invite-btn">Invite</button>' },
      initialState: { auth: { account: makeAccount(), permissions: [] } },
    })
    await flushPromises()

    expect(result.wrapper.find('[data-testid="invite-btn"]').exists()).toBe(true)
  })
})
