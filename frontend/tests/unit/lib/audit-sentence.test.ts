import { describe, expect, it, vi } from 'vitest'
import i18n from '@/plugins/i18n'
import { auditSentence } from '@/lib/audit-sentence'
import type { AuditEntryDto } from '@/api/types'
import { makeAuditEntry } from '../../factories/organization'

const owner = { id: 'r1', name: 'Owner', systemKind: 'Owner' }
const member = { id: 'r2', name: 'Member', systemKind: 'Member' }
const lead = { id: 'r3', name: 'Trưởng IT', systemKind: null }

function sentence(entry: Partial<AuditEntryDto>, locale: 'vi' | 'en' = 'vi') {
  i18n.global.locale.value = locale
  return auditSentence(makeAuditEntry(entry), {
    t: i18n.global.t,
    te: i18n.global.te,
    organizationName: 'Acme Inc',
  })
}

const CASES: [AuditEntryDto['type'], Record<string, unknown>, string, string][] = [
  [
    'InvitationSent',
    { email: 'a@x.vn', role: lead },
    'đã gửi lời mời tới a@x.vn (vai trò Trưởng IT)',
    'sent an invitation to a@x.vn (role Trưởng IT)',
  ],
  [
    'InvitationRevoked',
    { email: 'a@x.vn', role: owner },
    'đã thu hồi lời mời tới a@x.vn (vai trò Chủ sở hữu)',
    'revoked the invitation to a@x.vn (role Owner)',
  ],
  [
    'InvitationDeleted',
    { email: 'a@x.vn', role: member },
    'đã xoá lời mời đã hết hạn tới a@x.vn (vai trò Member)',
    'deleted the expired invitation to a@x.vn (role Member)',
  ],
  [
    'MemberJoined',
    { accountName: 'Bình', email: 'b@x.vn', role: member },
    '(b@x.vn) đã vào Acme Inc với vai trò Member',
    '(b@x.vn) joined Acme Inc with the role Member',
  ],
  [
    'MemberRolesChanged',
    { accountName: 'Bình', email: 'b@x.vn', rolesBefore: [member], rolesAfter: [owner, lead] },
    'đã đổi vai trò của Bình (b@x.vn): Member → Chủ sở hữu, Trưởng IT',
    'changed the roles of Bình (b@x.vn): Member → Owner, Trưởng IT',
  ],
  [
    'MemberRemoved',
    { accountName: 'Bình', email: 'b@x.vn', roles: [member, lead] },
    'đã gỡ Bình (b@x.vn) khỏi tổ chức, vai trò trước đó: Member, Trưởng IT',
    'removed Bình (b@x.vn) from the organization, previous roles: Member, Trưởng IT',
  ],
  [
    'MemberLeft',
    { accountName: 'Bình', email: 'b@x.vn' },
    '(b@x.vn) đã rời tổ chức',
    '(b@x.vn) left the organization',
  ],
  [
    'RoleCreated',
    { role: lead, permissionCodes: ['organizations.manage', 'auditlogs.view'] },
    'đã tạo vai trò "Trưởng IT" với quyền Quản lý tổ chức, Xem nhật ký kiểm toán',
    'created the role "Trưởng IT" with permissions Manage organization, View audit log',
  ],
  [
    'RoleUpdated',
    {
      nameBefore: 'IT',
      nameAfter: 'IT',
      permissionsAdded: [],
      permissionsRemoved: [],
    },
    'đã sửa vai trò "IT"',
    'edited the role "IT"',
  ],
  ['RoleDeleted', { role: lead }, 'đã xoá vai trò "Trưởng IT"', 'deleted the role "Trưởng IT"'],
  [
    'OrganizationRenamed',
    { nameBefore: 'Acme', nameAfter: 'Acme Inc' },
    'đã đổi tên tổ chức: "Acme" → "Acme Inc"',
    'renamed the organization: "Acme" → "Acme Inc"',
  ],
  ['OrganizationDeactivated', {}, 'đã vô hiệu hoá tổ chức', 'deactivated the organization'],
  ['OrganizationReactivated', {}, 'đã kích hoạt lại tổ chức', 'reactivated the organization'],
]

describe('auditSentence', () => {
  it.each(CASES)('builds the %s sentence in vi and en', (type, parameters, vi, en) => {
    expect(sentence({ type, parameters }, 'vi').text).toBe(vi)
    expect(sentence({ type, parameters }, 'en').text).toBe(en)
  })

  it('names the actor from actorName', () => {
    expect(sentence({ actorName: 'Nguyễn Văn A' }).actor).toBe('Nguyễn Văn A')
  })

  it('names a system administrator actor without a name', () => {
    const entry = { actorKind: 'SystemAdministrator', actorName: null } as const
    expect(sentence(entry, 'vi').actor).toBe('admin hệ thống FeedbackHub')
    expect(sentence(entry, 'en').actor).toBe('FeedbackHub system administrator')
  })

  it('names a deleted account actor', () => {
    const entry = { actorKind: 'Account', actorName: null } as const
    expect(sentence(entry, 'vi').actor).toBe('một tài khoản đã xoá')
    expect(sentence(entry, 'en').actor).toBe('a deleted account')
  })

  it('uses the generic sentence for an unknown type and never prints raw parameters', () => {
    const result = sentence({
      type: 'FeedbackArchived' as AuditEntryDto['type'],
      parameters: { secret: 'do-not-print', nested: { a: 1 } },
    })
    expect(result.text).toBe('đã thực hiện một thao tác quản trị')
    expect(result.text).not.toContain('do-not-print')
    expect(JSON.stringify(result)).not.toContain('do-not-print')
  })

  it('uses the generic sentence when a declared parameter has the wrong shape', () => {
    const wrongShapes: Record<string, unknown>[] = [
      { email: 42, role: member },
      { email: 'a@x.vn' },
      { email: 'a@x.vn', role: 'Member' },
      { email: 'a@x.vn', role: { id: 'r', name: 7, systemKind: null } },
      { email: 'a@x.vn', role: { id: 'r', name: 'X', systemKind: 'Root' } },
    ]
    for (const parameters of wrongShapes) {
      expect(sentence({ type: 'InvitationSent', parameters }).text).toBe(
        'đã thực hiện một thao tác quản trị',
      )
    }
    expect(
      sentence({
        type: 'MemberRemoved',
        parameters: { accountName: 'B', email: 'b@x.vn', roles: [member, 'Member'] },
      }).text,
    ).toBe('đã thực hiện một thao tác quản trị')
  })

  it('ignores parameters the catalogue does not declare for the type', () => {
    const t = vi.fn((key: string) => key)
    auditSentence(
      makeAuditEntry({
        type: 'OrganizationRenamed',
        parameters: { nameBefore: 'A', nameAfter: 'B', ipAddress: '10.0.0.1', reason: 'x' },
      }),
      { t, te: () => true, organizationName: 'Acme Inc' },
    )
    expect(t).toHaveBeenCalledWith('auditLog.events.OrganizationRenamed', {
      nameBefore: 'A',
      nameAfter: 'B',
      organization: 'Acme Inc',
    })
  })

  it('lists permission names in the interface language', () => {
    const parameters = { role: lead, permissionCodes: ['organizations.members.manage'] }
    expect(sentence({ type: 'RoleCreated', parameters }, 'vi').text).toContain('Quản lý thành viên')
    expect(sentence({ type: 'RoleCreated', parameters }, 'en').text).toContain('Manage members')
  })

  it('says a role has no permissions when the list is empty', () => {
    const parameters = { role: lead, permissionCodes: [] }
    expect(sentence({ type: 'RoleCreated', parameters }, 'vi').text).toBe(
      'đã tạo vai trò "Trưởng IT" với quyền (không có)',
    )
  })

  it('keeps an unlisted permission code as it is', () => {
    const parameters = { role: lead, permissionCodes: ['feedback.read'] }
    expect(sentence({ type: 'RoleCreated', parameters }).text).toContain('feedback.read')
  })

  it('composes RoleUpdated from rename, added and removed parts', () => {
    const base = { nameBefore: 'IT', nameAfter: 'IT', permissionsAdded: [], permissionsRemoved: [] }
    const type = 'RoleUpdated'

    expect(sentence({ type, parameters: { ...base, nameAfter: 'Kỹ thuật' } }, 'vi').text).toBe(
      'đã sửa vai trò "IT", đổi tên thành "Kỹ thuật"',
    )
    expect(
      sentence({ type, parameters: { ...base, permissionsAdded: ['auditlogs.view'] } }, 'vi').text,
    ).toBe('đã sửa vai trò "IT", thêm quyền Xem nhật ký kiểm toán')
    expect(
      sentence({ type, parameters: { ...base, permissionsRemoved: ['files.manage'] } }, 'vi').text,
    ).toBe('đã sửa vai trò "IT", bỏ quyền Quản lý file')
    expect(
      sentence(
        {
          type,
          parameters: {
            nameBefore: 'IT',
            nameAfter: 'Kỹ thuật',
            permissionsAdded: ['auditlogs.view'],
            permissionsRemoved: ['files.manage', 'settings.manage'],
          },
        },
        'en',
      ).text,
    ).toBe(
      'edited the role "IT", renamed to "Kỹ thuật", added permissions View audit log, removed permissions Manage files, Manage settings',
    )
  })

  it('keeps markup in names as plain text', () => {
    const name = '<img src=x onerror=alert(1)>'
    const result = sentence({
      actorName: name,
      type: 'RoleDeleted',
      parameters: { role: { id: 'r', name, systemKind: null } },
    })
    expect(result.actor).toBe(name)
    expect(result.text).toBe(`đã xoá vai trò "${name}"`)
  })
})
