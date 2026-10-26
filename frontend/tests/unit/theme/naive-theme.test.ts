import { describe, expect, it } from 'vitest'
import themeOverrides from '@/theme/naive-theme'
import { tokens } from '@/theme/tokens'

describe('naive theme', () => {
  it.each(['light', 'dark'] as const)(
    'takes primary, error, success, warning and info colours from the %s tokens',
    (mode) => {
      const common = themeOverrides[mode].common!
      const color = tokens[mode].color
      expect(common.primaryColor).toBe(color.accent)
      expect(common.primaryColorHover).toBe(color.accentHover)
      expect(common.primaryColorPressed).toBe(color.accentPressed)
      expect(common.errorColor).toBe(color.dangerSolid)
      expect(common.errorColorHover).toBe(color.dangerSolidHover)
      expect(common.errorColorPressed).toBe(color.dangerSolidPressed)
      expect(common.successColor).toBe(color.success)
      expect(common.warningColor).toBe(color.warning)
      expect(common.infoColor).toBe(color.info)
      expect(common.baseColor).toBe(color.onAccent)
    },
  )

  it.each(['light', 'dark'] as const)(
    'uses solid raised colours for popover, modal and card in %s',
    (mode) => {
      const common = themeOverrides[mode].common!
      const { raised, page, sunken } = tokens[mode].color
      expect(common.popoverColor).toBe(raised)
      expect(common.modalColor).toBe(raised)
      expect(common.cardColor).toBe(raised)
      expect(common.bodyColor).toBe(page)
      expect(common.inputColor).toBe(sunken)
      expect(common.tagColor).toBe(sunken)
      expect(common.buttonColor2).toBe(sunken)
    },
  )

  it('sizes buttons and inputs at 32, 40 and 44 px', () => {
    for (const mode of ['light', 'dark'] as const) {
      const common = themeOverrides[mode].common!
      expect(common.heightSmall).toBe('2rem')
      expect(common.heightMedium).toBe('2.5rem')
      expect(common.heightLarge).toBe('2.75rem')
    }
  })

  it('uses the self-hosted font families', () => {
    for (const mode of ['light', 'dark'] as const) {
      const common = themeOverrides[mode].common!
      expect(common.fontFamily).toBe(tokens.font.sans)
      expect(common.fontFamilyMono).toBe(tokens.font.mono)
    }
  })
})
