import type { GlobalThemeOverrides } from 'naive-ui'
import { tokens, type ThemeMode } from './tokens'

// Sizes are in rem so they follow useFontSize's root font-size toggle.
function overridesFor(mode: ThemeMode): GlobalThemeOverrides {
  const c = tokens[mode].color
  return {
    common: {
      fontFamily: tokens.font.sans,
      fontFamilyMono: tokens.font.mono,
      primaryColor: c.accent,
      primaryColorHover: c.accentHover,
      primaryColorPressed: c.accentPressed,
      primaryColorSuppl: c.accent,
      errorColor: c.dangerSolid,
      errorColorHover: c.dangerSolidHover,
      errorColorPressed: c.dangerSolidPressed,
      errorColorSuppl: c.dangerSolid,
      successColor: c.success,
      successColorHover: c.success,
      successColorPressed: c.success,
      successColorSuppl: c.success,
      warningColor: c.warning,
      warningColorHover: c.warning,
      warningColorPressed: c.warning,
      warningColorSuppl: c.warning,
      infoColor: c.info,
      infoColorHover: c.info,
      infoColorPressed: c.info,
      infoColorSuppl: c.info,
      baseColor: c.onAccent,
      bodyColor: c.page,
      cardColor: c.raised,
      popoverColor: c.raised,
      modalColor: c.raised,
      inputColor: c.sunken,
      inputColorDisabled: c.sunken,
      tagColor: c.sunken,
      buttonColor2: c.sunken,
      buttonColor2Hover: c.secondaryHover,
      buttonColor2Pressed: c.secondaryPressed,
      borderColor: c.control,
      dividerColor: c.line,
      textColorBase: c.ink,
      textColor1: c.ink,
      textColor2: c.ink,
      textColor3: c.inkMuted,
      textColorDisabled: c.inkDisabled,
      placeholderColor: c.inkMuted,
      placeholderColorDisabled: c.inkDisabled,
      borderRadius: '0.75rem',
      borderRadiusSmall: '0.5rem',
      heightSmall: '2rem',
      heightMedium: '2.5rem',
      heightLarge: '2.75rem',
    },
    Dialog: {
      padding: '1.5rem 2rem 1.75rem 2rem',
      borderRadius: '1.25rem',
    },
    // Every n-alert here is a one-line message under a form field; NAlert has no `size` prop.
    Alert: {
      padding: '0.5rem 0.75rem',
      fontSize: '0.8125rem',
      lineHeight: '1.4',
    },
  }
}

export default { light: overridesFor('light'), dark: overridesFor('dark') }
