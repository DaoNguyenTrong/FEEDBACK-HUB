import { describe, expect, it } from 'vitest'
import i18n from '@/plugins/i18n'
import { roleDisplayName } from '@/lib/role-name'

describe('roleDisplayName', () => {
  it('names system roles from systemKind in the interface language', () => {
    const role = { name: 'Owner', systemKind: 'Owner' as const }

    i18n.global.locale.value = 'vi'
    expect(roleDisplayName(role, i18n.global.t)).toBe('Chủ sở hữu')

    i18n.global.locale.value = 'en'
    expect(roleDisplayName(role, i18n.global.t)).toBe('Owner')
  })

  it('keeps the stored name of a custom role', () => {
    const t = (key: string) => `t:${key}`
    expect(roleDisplayName({ name: 'Trưởng IT', systemKind: null }, t)).toBe('Trưởng IT')
    expect(roleDisplayName({ name: 'Trưởng IT' }, t)).toBe('Trưởng IT')
  })
})
