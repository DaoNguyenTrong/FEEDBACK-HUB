import type { OrganizationDto } from '@/api/types'

// An organization the signed-in account owns, with every management permission.
export function makeOrganization(overrides: Partial<OrganizationDto> = {}): OrganizationDto {
  return {
    id: 'org-1',
    name: 'Acme Inc',
    slug: 'acme',
    status: true,
    myRoleNames: ['Owner'],
    myRoles: [{ id: 'role-owner', name: 'Owner', systemKind: 'Owner' }],
    myPermissionCodes: [
      'organizations.manage',
      'organizations.members.manage',
      'organizations.roles.manage',
    ],
    ...overrides,
  }
}
