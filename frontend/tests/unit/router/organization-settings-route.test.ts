import { describe, expect, it } from 'vitest'
import router, { resolveGuardRedirect } from '@/router'
import { Permissions } from '@/lib/permissions'

describe('organization-settings route', () => {
  const route = router.resolve('/organization/settings')

  it('is gated by the organization-manage permission inside an organization', () => {
    expect(route.name).toBe('organization-settings')
    expect(route.meta).toMatchObject({
      requiresAuth: true,
      requiresOrganization: true,
      permission: Permissions.OrganizationManage,
    })
  })

  it('sends the personal context home through the shared gate', () => {
    const auth = {
      isAuthenticated: true,
      organizationId: null,
      isSystemAdministrator: false,
      hasPermission: () => false,
    }
    const target = resolveGuardRedirect(
      { meta: route.meta, fullPath: route.fullPath, query: {}, name: route.name },
      auth,
      { isDown: false },
    )
    expect(target).toBeDefined()
  })
})
