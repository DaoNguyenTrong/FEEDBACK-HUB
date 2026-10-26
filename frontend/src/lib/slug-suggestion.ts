const SLUG_MAX_LENGTH = 50

export function suggestSlug(name: string): string {
  const folded = name.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase()
  return folded
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, SLUG_MAX_LENGTH)
    .replace(/-+$/, '')
}

export function normalizeSlugInput(value: string): string {
  return value.toLowerCase()
}
