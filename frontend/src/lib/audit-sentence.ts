import type { AuditEntryDto } from '@/api/types'
import { permissionName } from './permission-label'
import { roleDisplayName } from './role-name'

type Translate = (key: string, values?: Record<string, unknown>) => string

interface SentenceContext {
  t: Translate
  te: (key: string) => boolean
  organizationName: string
}

// `permissions` is a list of permission codes: shown by name, in the interface language.
type ParameterKind = 'text' | 'textList' | 'role' | 'roleList' | 'permissions'

// TECH00 §3.7: the declared parameters of every event type. A parameter not listed here never
// reaches a sentence, and a type not listed here gets the generic sentence.
const DECLARED: Record<string, Record<string, ParameterKind>> = {
  InvitationSent: { email: 'text', role: 'role' },
  InvitationRevoked: { email: 'text', role: 'role' },
  InvitationDeleted: { email: 'text', role: 'role' },
  MemberJoined: { accountName: 'text', email: 'text', role: 'role' },
  MemberRolesChanged: {
    accountName: 'text',
    email: 'text',
    rolesBefore: 'roleList',
    rolesAfter: 'roleList',
  },
  MemberRemoved: { accountName: 'text', email: 'text', roles: 'roleList' },
  MemberLeft: { accountName: 'text', email: 'text' },
  RoleCreated: { role: 'role', permissionCodes: 'permissions' },
  RoleUpdated: {
    nameBefore: 'text',
    nameAfter: 'text',
    permissionsAdded: 'permissions',
    permissionsRemoved: 'permissions',
  },
  RoleDeleted: { role: 'role' },
  OrganizationRenamed: { nameBefore: 'text', nameAfter: 'text' },
  OrganizationDeactivated: {},
  OrganizationReactivated: {},
}

const SYSTEM_KINDS = new Set(['Owner', 'Admin', 'Member'])

interface RoleRef {
  name: string
  systemKind: 'Owner' | 'Admin' | 'Member' | null
}

function isTextList(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string')
}

function asRole(value: unknown): RoleRef | null {
  if (typeof value !== 'object' || value === null) return null
  const { name, systemKind } = value as Record<string, unknown>
  if (typeof name !== 'string') return null
  if (systemKind === null || systemKind === undefined) return { name, systemKind: null }
  if (typeof systemKind === 'string' && SYSTEM_KINDS.has(systemKind)) {
    return { name, systemKind: systemKind as RoleRef['systemKind'] }
  }
  return null
}

function asRoleList(value: unknown): RoleRef[] | null {
  if (!Array.isArray(value)) return null
  const roles = value.map(asRole)
  return roles.every((role) => role !== null) ? (roles as RoleRef[]) : null
}

// Returns the text of one parameter, or null when its value is not of the declared shape.
function textOf(kind: ParameterKind, value: unknown, ctx: SentenceContext): string | null {
  switch (kind) {
    case 'text':
      return typeof value === 'string' ? value : null
    case 'textList':
      return isTextList(value) ? value.join(', ') : null
    case 'role': {
      const role = asRole(value)
      return role ? roleDisplayName(role, ctx.t) : null
    }
    case 'roleList': {
      const roles = asRoleList(value)
      return roles ? roles.map((role) => roleDisplayName(role, ctx.t)).join(', ') : null
    }
    case 'permissions':
      if (!isTextList(value)) return null
      return value.length === 0
        ? ctx.t('auditLog.noPermissions')
        : value.map((code) => permissionName(code, ctx.t)).join(', ')
  }
}

function sentenceParams(
  entry: AuditEntryDto,
  declared: Record<string, ParameterKind>,
  ctx: SentenceContext,
): Record<string, string> | null {
  const params: Record<string, string> = { organization: ctx.organizationName }
  for (const [key, kind] of Object.entries(declared)) {
    const text = textOf(kind, entry.parameters[key], ctx)
    if (text === null) return null
    params[key] = text
  }
  return params
}

// RoleUpdated is built from up to four parts: the edit itself, then each change that happened.
function addRoleUpdateParts(
  params: Record<string, string>,
  entry: AuditEntryDto,
  ctx: SentenceContext,
) {
  const { nameBefore, nameAfter } = params
  const listed = (key: string) => {
    const value = entry.parameters[key]
    return isTextList(value) ? value : []
  }

  params.renamed = nameBefore === nameAfter ? '' : ctx.t('auditLog.roleRenamed', { nameAfter })
  params.added =
    listed('permissionsAdded').length > 0
      ? ctx.t('auditLog.roleAdded', { permissions: params.permissionsAdded })
      : ''
  params.removed =
    listed('permissionsRemoved').length > 0
      ? ctx.t('auditLog.roleRemoved', { permissions: params.permissionsRemoved })
      : ''
}

/**
 * The actor and the sentence of one audit entry. Built only from the parameters declared for the
 * entry's type; raw `parameters` are never printed. Both parts are plain text for vue-i18n to
 * interpolate, never HTML (B6).
 */
export function auditSentence(
  entry: AuditEntryDto,
  ctx: SentenceContext,
): { actor: string; text: string } {
  const actor =
    entry.actorKind === 'SystemAdministrator'
      ? ctx.t('auditLog.systemAdministrator')
      : (entry.actorName ?? ctx.t('auditLog.deletedAccount'))

  const declared = Object.hasOwn(DECLARED, entry.type) ? DECLARED[entry.type] : undefined
  const key = `auditLog.events.${entry.type}`
  const params = declared && ctx.te(key) ? sentenceParams(entry, declared, ctx) : null
  if (!params) return { actor, text: ctx.t('auditLog.unknownEvent') }

  if (entry.type === 'RoleUpdated') addRoleUpdateParts(params, entry, ctx)
  return { actor, text: ctx.t(key, params) }
}
