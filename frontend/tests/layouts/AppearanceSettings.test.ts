import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import i18n from '@/plugins/i18n'
import AppearanceSettings from '@/layouts/AppearanceSettings.vue'
import { DEFAULT_FONT_SIZE, setFontSize } from '@/lib/font-size'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'

const t = i18n.global.t

async function render() {
  const result = await renderComponent(AppearanceSettings, {
    initialState: { layout: { settingsOpen: true } },
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

describe('AppearanceSettings', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setFontSize(DEFAULT_FONT_SIZE)
  })

  it('offers only colour mode, language and font size', async () => {
    const { wrapper } = await render()

    const headings = wrapper.findAll('h3').map((heading) => heading.text())
    expect(headings).toEqual([
      t('layout.settings.colorMode'),
      t('common.language'),
      t('common.fontSize'),
    ])
    expect(wrapper.find('[data-testid^="appearance-mode-"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid^="appearance-menu-theme-"]').exists()).toBe(false)
  })

  it('switches the language', async () => {
    const { wrapper, pinia } = await render()
    const locale = storeIn(pinia, useLocaleStore)
    expect(locale.currentLocale).toBe('en')

    await wrapper.get('[data-testid="appearance-locale-vi"]').trigger('click')

    expect(locale.currentLocale).toBe('vi')
    expect(wrapper.get('[data-testid="appearance-locale-vi"]').attributes('aria-pressed')).toBe(
      'true',
    )
    expect(wrapper.get('[data-testid="appearance-locale-en"]').attributes('aria-pressed')).toBe(
      'false',
    )
  })

  it('sets the system color mode', async () => {
    const { wrapper } = await render()

    await wrapper.get('[data-testid="appearance-theme-system"]').trigger('click')

    expect(localStorage.getItem('theme-preference')).toBe('system')
    expect(useThemePreference().preference.value).toBe('system')
    expect(wrapper.get('[data-testid="appearance-theme-system"]').attributes('aria-pressed')).toBe(
      'true',
    )
  })

  it('steps the font size within 14 to 20', async () => {
    setFontSize(19)
    const { wrapper } = await render()
    const increase = () => wrapper.get(`button[aria-label="${t('common.increaseFontSize')}"]`)

    await increase().trigger('click')

    expect(wrapper.text()).toContain('20px')
    expect(increase().attributes('disabled')).toBeDefined()
  })
})
