import { Permissions } from './permissions'

const KNOWN_CODES = new Set<string>(Object.values(Permissions))

// Locale keys use the code with dots replaced by `_`, e.g. `permissions.organizations_members_manage`.
function localeKey(code: string, part: 'name' | 'description'): string {
  return `permissions.${code.replaceAll('.', '_')}.${part}`
}

// Falls back to the raw permission code for anything outside `Permissions`, so an unlisted
// backend permission renders instead of crashing the UI.
export function permissionName(code: string, t: (key: string) => string): string {
  return KNOWN_CODES.has(code) ? t(localeKey(code, 'name')) : code
}

export function permissionDescription(code: string, t: (key: string) => string): string {
  return KNOWN_CODES.has(code) ? t(localeKey(code, 'description')) : ''
}
