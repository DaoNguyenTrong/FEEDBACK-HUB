import { describe, expect, it, vi } from 'vitest'
import i18n from '@/plugins/i18n'
import { permissionDescription, permissionName } from '@/lib/permission-label'
import { Permissions } from '@/lib/permissions'

const translate = (key: string) => i18n.global.t(key)

describe('permission names', () => {
  it('reads permissions.<code with underscores>.name and .description', () => {
    const t = vi.fn((key: string) => `translated:${key}`)

    expect(permissionName(Permissions.OrganizationMembersManage, t)).toBe(
      'translated:permissions.organizations_members_manage.name',
    )
    expect(permissionDescription(Permissions.OrganizationMembersManage, t)).toBe(
      'translated:permissions.organizations_members_manage.description',
    )
  })

  it('returns the code as the name and an empty description for an unknown code, without calling t', () => {
    const t = vi.fn((key: string) => key)

    expect(permissionName('billing.manage', t)).toBe('billing.manage')
    expect(permissionDescription('billing.manage', t)).toBe('')
    expect(t).not.toHaveBeenCalled()
  })

  // AC-294, AC-327
  it.each(['vi', 'en'] as const)(
    'gives every permission code a %s name and a description of 1 to 120 characters',
    (locale) => {
      i18n.global.locale.value = locale

      for (const code of Object.values(Permissions)) {
        const name = permissionName(code, translate)
        const description = permissionDescription(code, translate)

        expect(name).not.toBe(code)
        expect(name.trim()).not.toBe('')
        expect(description.length).toBeGreaterThanOrEqual(1)
        expect(description.length).toBeLessThanOrEqual(120)
      }
    },
  )

  it('uses the approved vi wording for the roles permission', () => {
    i18n.global.locale.value = 'vi'

    expect(permissionName(Permissions.OrganizationRolesManage, translate)).toBe('Quản lý vai trò')
    expect(permissionDescription(Permissions.OrganizationRolesManage, translate)).toBe(
      'Tạo, sửa và xoá các vai trò tự tạo.',
    )
  })
})
