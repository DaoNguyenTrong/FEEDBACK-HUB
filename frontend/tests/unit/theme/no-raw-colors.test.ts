import { describe, expect, it } from 'vitest'

const ALLOWED = [
  'components/GoogleLoginButton.vue',
  'components/MicrosoftLoginButton.vue',
]

const EXCLUDED = /^\/src\/(theme|assets|api\/generated|typings|locales)\//

const sources = import.meta.glob('/src/**/*.{vue,ts}', {
  query: '?raw',
  import: 'default',
  eager: true,
})

const PHASE_7_FILES: string[] = [
  'components/InlineNotice.vue',
  'components/InlineConfirm.vue',
  'components/AppSheet.vue',
  'components/ProfilePanel.vue',
  'components/CreateOrganizationSheet.vue',
  'components/EmptyState.vue',
  'components/StatusLabel.vue',
  'components/AmbientBackdrop.vue',
  'layouts/AppRail.vue',
  'layouts/AppRailNav.vue',
  'layouts/AppTopBar.vue',
  'layouts/OrgSwitcher.vue',
  'layouts/OrgSwitcherPanel.vue',
  'layouts/AppFooter.vue',
  'layouts/UserMenu.vue',
  'layouts/AppLayout.vue',
  'components/AccessProblemState.vue',
  'components/NotificationBell.vue',
  'layouts/AppearanceSettings.vue',
  'views/NotFoundView.vue',
  'views/WelcomeView.vue',
  'components/OrganizationRows.vue',
]

const RAW_COLOR = /#[0-9a-fA-F]{3,8}\b|(?<![a-z])(?:rgba?|hsla?)\(/
const PALETTE = [
  'slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue',
  'indigo|violet|purple|fuchsia|pink|rose',
].join('|')
const ROLE_UTILITY_PREFIX = '(?:bg|text|border|ring|fill|stroke|from|via|to|outline|divide|shadow)'
const LEGACY_RAMP = new RegExp(`\\b${ROLE_UTILITY_PREFIX}-(?:primary|surface|accent)-\\d`)
const TAILWIND_PALETTE = new RegExp(`\\b${ROLE_UTILITY_PREFIX}-(?:${PALETTE})-\\d`)
const BLACK_WHITE = new RegExp(`\\b${ROLE_UTILITY_PREFIX}-(?:white|black)\\b`)

describe('no raw colours', () => {
  it('keeps hex and rgba colours out of every source file except the brand logo buttons', () => {
    const offenders = Object.entries(sources)
      .filter(([path]) => !EXCLUDED.test(path))
      .filter(([path]) => !ALLOWED.some((allowed) => path.endsWith(`/src/${allowed}`)))
      .filter(([, content]) => RAW_COLOR.test(content as string))
      .map(([path]) => path)
    expect(offenders).toEqual([])
  })

  it('finds a source file for every phase 7 path, so a rename cannot drop coverage', () => {
    const matched = PHASE_7_FILES.filter((file) =>
      Object.keys(sources).some((path) => path.endsWith(`/src/${file}`)),
    )
    expect(matched).toEqual(PHASE_7_FILES)
  })

  it('keeps the phase 7 files on role colours only', () => {
    const offenders = Object.entries(sources)
      .filter(([path]) => PHASE_7_FILES.some((file) => path.endsWith(`/src/${file}`)))
      .filter(([, content]) => {
        const text = content as string
        return LEGACY_RAMP.test(text) || TAILWIND_PALETTE.test(text) || BLACK_WHITE.test(text)
      })
      .map(([path]) => path)
    expect(offenders).toEqual([])
  })
})
