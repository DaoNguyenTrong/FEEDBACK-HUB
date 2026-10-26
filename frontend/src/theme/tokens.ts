export type ThemeMode = 'light' | 'dark'

const lightColor = {
  page: '#E9F1FA',
  panel: 'rgba(255,255,255,.78)',
  panelSolid: '#FAFCFE',
  panelThin: 'rgba(255,255,255,.55)',
  panelThinSolid: '#F5F9FD',
  raised: '#FFFFFF',
  sunken: '#EEF1F7',
  line: 'rgba(28,26,51,.10)',
  control: '#7C7896',
  focus: '#5B3FD9',
  ink: '#1C1A33',
  inkMuted: '#56536F',
  inkDisabled: '#8E8BA6',
  accent: '#4F3D91',
  accentHover: '#43337F',
  accentPressed: '#37296B',
  onAccent: '#FFFFFF',
  accentSoft: '#ECE8F7',
  onAccentSoft: '#3E2F7A',
  secondaryHover: '#F1F3F8',
  secondaryPressed: '#E6E9F1',
  ghostHover: '#ECE8F7',
  ghostPressed: '#DFD8F2',
  success: '#256126',
  successSoft: '#DCF1D8',
  warning: '#7A4A00',
  warningSoft: '#FFE8C2',
  danger: '#A12A26',
  dangerSoft: '#FBDCDC',
  dangerSoftHover: '#F7C9C9',
  dangerSolid: '#B4232A',
  dangerSolidHover: '#9C1C23',
  dangerSolidPressed: '#84161C',
  onDangerSolid: '#FFFFFF',
  info: '#14598F',
  infoSoft: '#DCEDFB',
  priorityHigh: '#C2410C',
  brand: '#4F3D91',
  leaf: '#8CC640',
  statusNew: '#3E2F7A',
  statusNewSoft: '#ECE8F7',
  statusAccepted: '#36334F',
  statusAcceptedSoft: '#E7E9F2',
  statusNeedInfo: '#7A4A00',
  statusNeedInfoSoft: '#FFE8C2',
  statusInProgress: '#14598F',
  statusInProgressSoft: '#DCEDFB',
  statusInReview: '#0B5E63',
  statusInReviewSoft: '#D2F1EF',
  statusResolved: '#256126',
  statusResolvedSoft: '#DCF1D8',
  statusClosed: '#56536F',
  statusClosedSoft: '#F0F1F6',
  statusDuplicate: '#56536F',
  statusDuplicateSoft: '#F0F1F6',
  statusRejected: '#A12A26',
  statusRejectedSoft: '#FBDCDC',
}

export type ThemeColors = Record<keyof typeof lightColor, string>

const darkColor: ThemeColors = {
  page: '#0E1124',
  panel: 'rgba(36,33,68,.78)',
  panelSolid: '#1F1D3D',
  panelThin: 'rgba(30,28,58,.55)',
  panelThinSolid: '#171730',
  raised: '#2C2950',
  sunken: '#15132B',
  line: 'rgba(255,255,255,.10)',
  control: '#807CA6',
  focus: '#E3DBFF',
  ink: '#ECEAF7',
  inkMuted: '#A9A5C6',
  inkDisabled: '#6E6A90',
  accent: '#B4A6F2',
  accentHover: '#C4B9F6',
  accentPressed: '#A394E6',
  onAccent: '#1B1242',
  accentSoft: '#342D60',
  onAccentSoft: '#D4CBFA',
  secondaryHover: '#36325E',
  secondaryPressed: '#2A2649',
  ghostHover: '#302A57',
  ghostPressed: '#3A3368',
  success: '#9EDE99',
  successSoft: '#1B3B22',
  warning: '#F7C873',
  warningSoft: '#3D2F17',
  danger: '#FF9C97',
  dangerSoft: '#47202A',
  dangerSoftHover: '#57252F',
  dangerSolid: '#FF8A84',
  dangerSolidHover: '#FFA19C',
  dangerSolidPressed: '#F2716B',
  onDangerSolid: '#2A0A0D',
  info: '#9CCFF7',
  infoSoft: '#173452',
  priorityHigh: '#FF8A4C',
  brand: '#4F3D91',
  leaf: '#8CC640',
  statusNew: '#D4CBFA',
  statusNewSoft: '#342D60',
  statusAccepted: '#D6D3E8',
  statusAcceptedSoft: '#2F2C4B',
  statusNeedInfo: '#F7C873',
  statusNeedInfoSoft: '#3D2F17',
  statusInProgress: '#9CCFF7',
  statusInProgressSoft: '#173452',
  statusInReview: '#7FDDD8',
  statusInReviewSoft: '#12393D',
  statusResolved: '#9EDE99',
  statusResolvedSoft: '#1B3B22',
  statusClosed: '#A9A5C6',
  statusClosedSoft: '#27243F',
  statusDuplicate: '#A9A5C6',
  statusDuplicateSoft: '#27243F',
  statusRejected: '#FF9C97',
  statusRejectedSoft: '#47202A',
}

export interface GlassTokens {
  thin: { tint: string; solid: string; blur: '24px' }
  thick: { tint: string; solid: string; blur: '30px' }
}

export interface AmbientTokens {
  blobs: [string, string, string]
  full: number
  soft: number
}

function glassOf(color: ThemeColors): GlassTokens {
  return {
    thin: { tint: color.panelThin, solid: color.panelThinSolid, blur: '24px' },
    thick: { tint: color.panel, solid: color.panelSolid, blur: '30px' },
  }
}

const primaryRamp = [
  '#f5f0fe',
  '#e7ddfd',
  '#cfbcfb',
  '#ac8cf8',
  '#895bf5',
  '#7842f5',
  '#6428f4',
  '#5017d3',
  '#4418aa',
  '#31176d',
  '#1d0f3d',
]

const surfaceLight = [
  '#ffffff',
  '#eef7fe',
  '#daeefd',
  '#c9e2f5',
  '#b3cde3',
  '#94a3b8',
  '#64748b',
  '#475569',
  '#334155',
  '#1e293b',
  '#0f172a',
  '#020617',
]

const surfaceDark = [
  '#e2e1e6',
  '#dad9de',
  '#ceccd4',
  '#b8b6c0',
  '#a5a2b0',
  '#8f8b9d',
  '#79758a',
  '#555a75',
  '#3a3f58',
  '#262a3d',
  '#1e2235',
  '#121527',
]

const primarySteps = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]
const surfaceSteps = [0, 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]

function ramp(name: string, steps: number[], values: string[]): Record<string, string> {
  return Object.fromEntries(steps.map((step, i) => [`--color-${name}-${step}`, values[i] ?? '']))
}

const primaryDark = primaryRamp.map((value, i) => (primarySteps[i] === 200 ? '#a27aff' : value))

export const tokens = {
  light: {
    color: lightColor as ThemeColors,
    glass: glassOf(lightColor),
    ambient: { blobs: ['#A9D3F5', '#CBBEF0', '#CFE8A8'], full: 0.75, soft: 0.35 } as AmbientTokens,
  },
  dark: {
    color: darkColor,
    glass: glassOf(darkColor),
    ambient: { blobs: ['#3A2C7A', '#14406B', '#2E5418'], full: 0.55, soft: 0.3 } as AmbientTokens,
  },
  legacy: {
    light: {
      ...ramp('primary', primarySteps, primaryRamp),
      ...ramp('surface', surfaceSteps, surfaceLight),
    } as Record<string, string>,
    dark: {
      ...ramp('primary', primarySteps, primaryDark),
      ...ramp('surface', surfaceSteps, surfaceDark),
    } as Record<string, string>,
  },
  radius: { sm: '8px', md: '12px', lg: '16px', xl: '22px', full: '999px' } as const,
  motion: {
    quick: '160ms ease',
    panel: '240ms var(--ease-spring)',
    pop: '320ms var(--ease-spring)',
    spring: 'cubic-bezier(0.34, 1.36, 0.64, 1)',
  } as const,
  font: {
    sans: "'Plus Jakarta Sans Variable', system-ui, sans-serif",
    mono: "'JetBrains Mono Variable', ui-monospace, monospace",
  } as const,
}

const shadowPanel: Record<ThemeMode, string> = {
  light: 'rgba(0,39,67,0.08)',
  dark: 'rgba(0,0,0,.40)',
}

function kebab(key: string): string {
  return key.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)
}

export function cssVariables(mode: ThemeMode): Record<string, string> {
  return Object.fromEntries(
    Object.entries(tokens[mode].color).map(([key, value]) => [`--color-${kebab(key)}`, value]),
  )
}

export function cssRootVariables(mode: ThemeMode): Record<string, string> {
  const { blobs, full, soft } = tokens[mode].ambient
  return {
    '--ambient-1': blobs[0],
    '--ambient-2': blobs[1],
    '--ambient-3': blobs[2],
    '--ambient-full': String(full),
    '--ambient-soft': String(soft),
    '--shadow-panel': shadowPanel[mode],
  }
}
