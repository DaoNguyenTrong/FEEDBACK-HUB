import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { cssRootVariables, cssVariables, tokens } from '@/theme/tokens'

// Vitest serves .css imports as empty strings, so the stylesheets are read from disk.
const frontendRoot = resolve(import.meta.dirname, '../../..')

function readFrontend(path: string): string {
  return readFileSync(resolve(frontendRoot, path), 'utf8')
}

const css = readFrontend('src/assets/styles/tailwind.css')
const jakartaCss = readFrontend('node_modules/@fontsource-variable/plus-jakarta-sans/index.css')
const jetbrainsCss = readFrontend('node_modules/@fontsource-variable/jetbrains-mono/index.css')

function blockAt(source: string, open: number): string {
  const start = source.indexOf('{', open)
  let depth = 0
  for (let i = start; i < source.length; i++) {
    if (source[i] === '{') depth++
    if (source[i] === '}' && --depth === 0) return source.slice(start + 1, i)
  }
  throw new Error('unbalanced braces')
}

function block(source: string, header: string): string {
  const open = source.indexOf(header)
  if (open < 0) throw new Error(`missing block: ${header}`)
  return blockAt(source, open)
}

function normalize(value: string): string {
  return value
    .toLowerCase()
    .replace(/\s+/g, '')
    .replace(/\d*\.\d+/g, (n) => String(parseFloat(n)))
}

function declarations(body: string): Map<string, string> {
  const map = new Map<string, string>()
  for (const match of body.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) {
    const [, name, value] = match
    if (name && value) map.set(name, normalize(value))
  }
  return map
}

function expectDeclared(body: string, expected: Record<string, string>) {
  const found = declarations(body)
  for (const [name, value] of Object.entries(expected)) {
    expect(found.get(name), name).toBe(normalize(value))
  }
}

const themeBlock = block(css, '@theme static')
const darkBlock = block(css, '\n.dark {')
const rootBlock = block(css, '\n:root {')

describe('tokens', () => {
  it('declares every light variable in the @theme block with the token value', () => {
    expectDeclared(themeBlock, cssVariables('light'))
  })

  it('declares every dark variable in the .dark block with the token value', () => {
    expectDeclared(darkBlock, cssVariables('dark'))
  })

  it('declares the light non-colour variables in :root and the dark ones in .dark', () => {
    expectDeclared(rootBlock, cssRootVariables('light'))
    expectDeclared(darkBlock, cssRootVariables('dark'))
  })

  it('keeps the legacy primary and surface ramps equal to tokens.legacy', () => {
    expectDeclared(themeBlock, tokens.legacy.light)
    expectDeclared(darkBlock, tokens.legacy.dark)
  })

  it('declares radius, motion and font tokens in the @theme block', () => {
    const expected = {
      ...Object.fromEntries(Object.entries(tokens.radius).map(([k, v]) => [`--radius-${k}`, v])),
      '--ease-spring': tokens.motion.spring,
      '--font-sans': tokens.font.sans,
      '--font-mono': tokens.font.mono,
    }
    expectDeclared(themeBlock, expected)
  })

  it('declares the type scale in the @theme block', () => {
    const found = declarations(themeBlock)
    for (const name of ['page-title', 'section', 'row', 'body', 'meta', 'caption', 'code']) {
      expect(found.has(`--text-${name}`), name).toBe(true)
      expect(found.has(`--text-${name}--line-height`), name).toBe(true)
      expect(found.has(`--text-${name}--font-weight`), name).toBe(true)
    }
  })

  it('gives glass-thin a 24px blur and glass-thick a 30px blur', () => {
    expect(block(css, '@utility glass-thin')).toContain('blur(24px)')
    expect(block(css, '@utility glass-thick')).toContain('blur(30px)')
  })

  it('switches every glass utility to its solid colour under prefers-reduced-transparency', () => {
    const fallback = block(css, '@media (prefers-reduced-transparency: reduce)')
    const thin = block(fallback, '.glass-thin')
    const thick = block(fallback, '.glass-thick')
    expect(thin).toContain('background-color: var(--color-panel-thin-solid)')
    expect(thin).toContain('backdrop-filter: none')
    expect(thick).toContain('background-color: var(--color-panel-solid)')
    expect(thick).toContain('backdrop-filter: none')
  })

  it('switches every glass utility to its solid colour when backdrop-filter is unsupported', () => {
    const fallback = block(css, '@supports not (backdrop-filter: blur(1px))')
    const thin = block(fallback, '.glass-thin')
    const thick = block(fallback, '.glass-thick')
    expect(thin).toContain('background-color: var(--color-panel-thin-solid)')
    expect(thin).toContain('backdrop-filter: none')
    expect(thick).toContain('background-color: var(--color-panel-solid)')
    expect(thick).toContain('backdrop-filter: none')
  })

  it('hides the ambient backdrop under prefers-reduced-transparency', () => {
    const fallback = block(css, '@media (prefers-reduced-transparency: reduce)')
    expect(block(fallback, '[data-ambient]')).toContain('display: none')
  })

  it('hides the ambient backdrop when backdrop-filter is unsupported', () => {
    const fallback = block(css, '@supports not (backdrop-filter: blur(1px))')
    expect(block(fallback, '[data-ambient]')).toContain('display: none')
  })

  it('removes transitions and animations under prefers-reduced-motion', () => {
    const reduced = block(css, '@media (prefers-reduced-motion: reduce)')
    expect(reduced).toMatch(/transition-duration:\s*0s\s*!important/)
    expect(reduced).toMatch(/animation-duration:\s*0s\s*!important/)
  })

  it('matches the System board for the accent, focus and danger colours', () => {
    expect(tokens.light.color.accent).toBe('#4F3D91')
    expect(tokens.light.color.focus).toBe('#5B3FD9')
    expect(tokens.light.color.dangerSolid).toBe('#B4232A')
    expect(tokens.dark.color.accent).toBe('#B4A6F2')
    expect(tokens.dark.color.focus).toBe('#E3DBFF')
  })

  it('uses the family names the font packages declare', () => {
    expect(tokens.font.sans.startsWith("'Plus Jakarta Sans Variable'")).toBe(true)
    expect(tokens.font.mono.startsWith("'JetBrains Mono Variable'")).toBe(true)
    expect(jakartaCss).toContain("font-family: 'Plus Jakarta Sans Variable'")
    expect(jetbrainsCss).toContain("font-family: 'JetBrains Mono Variable'")
  })
})
