type SystemKind = 'Owner' | 'Admin' | 'Member'

export function roleDisplayName(
  role: { name: string; systemKind?: SystemKind | null },
  t: (key: string) => string,
): string {
  return role.systemKind ? t(`roles.systemKind.${role.systemKind}`) : role.name
}
