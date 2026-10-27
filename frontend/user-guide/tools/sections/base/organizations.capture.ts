import type { Page } from '@playwright/test'
import { locales, test } from '../../fixtures'

// "Tổ chức" chapter: the organization switcher and the create-organization panel, opened from the
// top bar. Nothing is created or switched.
const chapter = 'organizations'
const account = '[data-testid="app-header-user-menu"]'
const trigger = '[data-testid="org-switcher-trigger"]'
const firstOrganization = '[data-testid^="org-switcher-item-"] >> nth=0'
const welcome = '[data-testid="org-switcher-welcome"]'
const create = '[data-testid="org-switcher-create"]'

async function openSwitcher(page: Page) {
  await page.locator(trigger).click()
  await page.locator(create).waitFor()
}

for (const locale of locales) {
  test(`switch [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await openSwitcher(page)
    await capture.annotate([
      { selector: trigger, n: 1, at: 'below' },
      { selector: firstOrganization, n: 2, at: 'top-right' },
      { selector: welcome, n: 3, at: 'top-right' },
      { selector: create, n: 4, at: 'top-right' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'switch' })
  })

  test(`create [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await openSwitcher(page)
    await page.locator(create).click()
    await page.locator('[data-testid="create-org-name"]').waitFor()
    await page
      .locator('[data-testid="create-org-name"]')
      .fill(locale === 'vi' ? 'Phòng Kinh doanh' : 'Sales')
    await capture.annotate([
      { selector: '[data-testid="create-org-name"]', n: 1, at: 'top-right' },
      { selector: '[data-testid="create-org-slug"]', n: 2, at: 'top-right' },
      { selector: '[data-testid="create-org-submit"]', n: 3, at: 'before' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'create' })
  })
}
