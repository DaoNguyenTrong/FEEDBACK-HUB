import { describe, expect, it } from 'vitest'
import AppFooter from '@/layouts/AppFooter.vue'
import { renderComponent } from '../helpers/render'
import en from '@/locales/en'
import i18n from '@/plugins/i18n'

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// The footer only reflects the shared health store — polling is owned by App.vue —
// so these tests seed the store state rather than mock the /api/health request.

describe('AppFooter', () => {
  it('keeps the one-line footer with app name, version and connection dot below the content', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online' } },
    })

    const footer = wrapper.get('footer')
    expect(footer.classes()).toEqual(expect.arrayContaining(['border-line', 'text-ink-muted']))
    expect(footer.text()).toContain(i18n.global.t('app.name'))
    expect(footer.text()).toContain(i18n.global.t('app.version', { version: __APP_VERSION__ }))
    expect(footer.find('[role="status"]').exists()).toBe(true)
  })

  it('labels the connection dot with app.apiStatus.online when the API is up', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online' } },
    })

    expect(wrapper.get('[role="status"]').attributes('aria-label')).toBe(
      i18n.global.t('app.apiStatus.online'),
    )
    expect(i18n.global.t('app.apiStatus.online')).toBe('Connected')
  })

  it('renders the app name and version', async () => {
    const { wrapper } = await renderComponent(AppFooter)

    expect(wrapper.text()).toContain(en.app.name)
    expect(wrapper.text()).toContain(i18n.global.t('app.version', { version: __APP_VERSION__ }))
  })

  it('shows a neutral status dot while the health check is still "checking"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'checking' } },
    })

    const dot = wrapper.find('[role="status"]')
    expect(dot.classes()).toContain('bg-ink-disabled')
    expect(dot.classes()).not.toContain('bg-success')
    expect(dot.classes()).not.toContain('bg-danger')
  })

  it('shows the online status dot when the store reports "online"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online', apiVersion: '3.1.4' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-success')
    // API version is not shown in the footer (SA decision: remove API version and build time)
    expect(wrapper.text()).not.toContain(i18n.global.t('app.apiVersion', { version: '3.1.4' }))
  })

  it('shows a red status dot when the store reports "offline"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'offline' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-danger')
  })

  it('shows a red status dot when the store reports "error"', async () => {
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'error' } },
    })

    expect(wrapper.find('[role="status"]').classes()).toContain('bg-danger')
  })

  it('does not show the API build time even when the store has one', async () => {
    // SA decision: footer shows only app name, version, and status dot — no API build time.
    const { wrapper } = await renderComponent(AppFooter, {
      initialState: { health: { status: 'online', apiBuildTimestamp: '2026-02-02T00:00:00Z' } },
    })

    expect(wrapper.text()).not.toMatch(
      new RegExp(`${escapeRegExp(i18n.global.t('app.apiBuildTime', { time: '' }))}.*2026`),
    )
  })
})
