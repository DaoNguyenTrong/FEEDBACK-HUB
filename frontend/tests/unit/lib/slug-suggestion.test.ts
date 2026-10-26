import { describe, expect, it } from 'vitest'
import { normalizeSlugInput, suggestSlug } from '@/lib/slug-suggestion'

describe('suggestSlug', () => {
  it.each([
    ['Công ty Đông Á', 'cong-ty-dong-a'], // AC-281
    ['AB', 'ab'], // AC-328
    [
      'Công ty TNHH Thương mại Dịch vụ Kỹ thuật Hải Đăng Hà Nội',
      'cong-ty-tnhh-thuong-mai-dich-vu-ky-thuat-hai-dang', // AC-331, 49 characters
    ],
    ['  --Sao   Mai!! Tech--  ', 'sao-mai-tech'],
    ['Đường Ống', 'duong-ong'],
    ['', ''],
  ])('suggests %s → %s', (name, slug) => {
    expect(suggestSlug(name)).toBe(slug)
  })

  it('never ends in a hyphen after the cut to 50 characters', () => {
    const slug = suggestSlug(`${'a'.repeat(49)} b`)

    expect(slug).toBe('a'.repeat(49))
  })
})

describe('normalizeSlugInput', () => {
  it('lowercases typed capitals and changes nothing else', () => {
    expect(normalizeSlugInput('Sao-Mai')).toBe('sao-mai') // AC-329
    expect(normalizeSlugInput('sao_mai')).toBe('sao_mai')
  })
})
