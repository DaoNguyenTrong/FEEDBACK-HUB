import { describe, expect, test } from 'bun:test'
import { existsSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { generateBrandTokensCss } from '../scripts/brand-tokens'
import { copyLogos } from '../scripts/sync-brand'

const fixture = await Bun.file(new URL('./fixtures/tailwind.css', import.meta.url)).text()
const appCss = await Bun.file(
  new URL('../../../src/assets/styles/tailwind.css', import.meta.url),
).text()

describe('generateBrandTokensCss', () => {
  test('emits every brand token for light and dark', () => {
    const css = generateBrandTokensCss(fixture)
    const [lightBlock, darkBlock] = css.split('.dark {')
    for (const name of ['accent', 'accent-soft', 'on-chrome', 'chrome', 'deep', 'leaf', 'page']) {
      expect(lightBlock).toContain(`--brand-${name}:`)
      expect(darkBlock).toContain(`--brand-${name}:`)
    }
  })

  test('resolves chained var() to a hex value', () => {
    expect(generateBrandTokensCss(fixture)).toContain('--brand-accent-hover: #123456;')
  })

  test('takes each mode from its own block', () => {
    const [lightBlock, darkBlock] = generateBrandTokensCss(fixture).split('.dark {')
    expect(lightBlock).toContain('--brand-accent: #4f3d91;')
    expect(darkBlock).toContain('--brand-accent: #b4a6f2;')
    expect(lightBlock).toContain('--brand-page: #e9f1fa;')
    expect(darkBlock).toContain('--brand-page: #0e1124;')
  })

  test('keeps the sidebar, hero and table chrome dark and its text light in both modes', () => {
    const [lightBlock, darkBlock] = generateBrandTokensCss(fixture).split('.dark {')
    expect(lightBlock).toContain('--brand-chrome: #37296b;')
    expect(darkBlock).toContain('--brand-chrome: #15132b;')
    expect(lightBlock).toContain('--brand-on-chrome: #eceaf7;')
    expect(darkBlock).toContain('--brand-on-chrome: #eceaf7;')
    expect(lightBlock).toContain('--brand-accent-fill: #4f3d91;')
    expect(darkBlock).toContain('--brand-accent-fill: #4f3d91;')
  })

  test('names every missing token', () => {
    const broken = fixture
      .replace(/--color-leaf:[^;]+;/, '')
      .replace(/--color-accent-soft:[^;]+;/, '')
    expect(() => generateBrandTokensCss(broken)).toThrow(
      /--color-accent-soft.*--color-leaf|--color-leaf.*--color-accent-soft/,
    )
  })

  test('reads an app stylesheet whose theme block is plain @theme', () => {
    const plain = fixture.replace('@theme static {', '@theme {')
    expect(generateBrandTokensCss(plain)).toContain('--brand-accent: #4f3d91;')
  })

  // Only the shape, never the values: a fork recolours the app without touching this base file.
  test('the real app stylesheet yields every brand token in both modes', () => {
    const css = generateBrandTokensCss(appCss)
    const names = [...css.matchAll(/--brand-([\w-]+):/g)].map((m) => m[1])
    expect(names).toHaveLength(36)
  })

  test('a fork that recolours the app still gets every brand token', () => {
    const recoloured = appCss.replace(/#[0-9a-f]{6}\b/gi, '#010203')
    expect(generateBrandTokensCss(recoloured)).toContain('--brand-accent: #010203;')
  })
})

describe('copyLogos', () => {
  test('refuses to copy anything when a source file is missing', () => {
    const from = mkdtempSync(join(tmpdir(), 'logos-'))
    const to = mkdtempSync(join(tmpdir(), 'brand-'))
    writeFileSync(join(from, 'light.webp'), 'x')
    const brand = { wordmarkLight: 'light.webp', wordmarkDark: 'dark.webp', mark: 'mark.webp' }
    expect(() => copyLogos(brand, from, to)).toThrow(/dark\.webp.*mark\.webp/)
    expect(existsSync(join(to, 'light.webp'))).toBe(false)
  })
})
