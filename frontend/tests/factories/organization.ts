import type {
  AuditEntryDto,
  CurrentOrganizationDto,
  InvitationDto,
  InvitationLookupDto,
  OrganizationDto,
  OrganizationMemberDto,
  PermissionDto,
  RoleDto,
  RoleOptionDto,
  RoleRefDto,
  SystemOrganizationDto,
} from '@/api/types'

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

export function makeCurrentOrganization(
  overrides: Partial<CurrentOrganizationDto> = {},
): CurrentOrganizationDto {
  return { id: 'org-1', name: 'Acme Inc', slug: 'acme', status: true, ...overrides }
}

export function makeRoleRef(overrides: Partial<RoleRefDto> = {}): RoleRefDto {
  return { id: 'role-member', name: 'Member', systemKind: 'Member', ...overrides }
}

export function makeRoleOption(overrides: Partial<RoleOptionDto> = {}): RoleOptionDto {
  return {
    id: 'role-member',
    name: 'Member',
    systemKind: 'Member',
    canGrant: true,
    ...overrides,
  }
}

export function makeRole(overrides: Partial<RoleDto> = {}): RoleDto {
  return {
    id: 'role-custom',
    name: 'Support',
    systemKind: null,
    isSystem: false,
    permissionCodes: ['organizations.members.manage'],
    holderCount: 0,
    pendingInvitationCount: 0,
    canEdit: true,
    canDelete: true,
    deleteBlockedReason: null,
    ...overrides,
  }
}

export function makePermission(code: string): PermissionDto {
  return { code }
}

export function makeMember(overrides: Partial<OrganizationMemberDto> = {}): OrganizationMemberDto {
  return {
    accountId: 'account-2',
    accountName: 'Trần Thị B',
    email: 'b@example.com',
    roleIds: ['role-member'],
    roleNames: ['Member'],
    roles: [makeRoleRef()],
    isSelf: false,
    canChangeRoles: true,
    canRemove: true,
    lockReason: null,
    lockRole: null,
    ...overrides,
  }
}

export function makeInvitation(overrides: Partial<InvitationDto> = {}): InvitationDto {
  return {
    id: 'invitation-1',
    email: 'new@example.com',
    role: makeRoleRef(),
    expiresAt: '2026-11-01T00:00:00Z',
    status: 'Pending',
    canRevokeOrDelete: true,
    canResend: true,
    lockReason: null,
    ...overrides,
  }
}

export function makeInvitationLookup(
  overrides: Partial<InvitationLookupDto> = {},
): InvitationLookupDto {
  return {
    status: 'Valid',
    organizationName: 'Acme Inc',
    role: makeRoleRef(),
    expiresAt: '2026-11-01T00:00:00Z',
    email: 'new@example.com',
    ...overrides,
  }
}

export function makeSystemOrganization(
  overrides: Partial<SystemOrganizationDto> = {},
): SystemOrganizationDto {
  return { id: 'org-1', name: 'Acme Inc', memberCount: 3, isDeactivated: false, ...overrides }
}

export function makeAuditEntry(overrides: Partial<AuditEntryDto> = {}): AuditEntryDto {
  return {
    id: 1,
    occurredAt: '2026-10-20T08:30:00Z',
    actorKind: 'Account',
    actorName: 'Nguyễn Văn A',
    type: 'MemberJoined',
    parameters: {},
    ...overrides,
  }
}
