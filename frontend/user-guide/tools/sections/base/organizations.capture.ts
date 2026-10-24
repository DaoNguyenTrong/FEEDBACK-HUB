import type { Page } from '@playwright/test'
import { locales, test } from '../../fixtures'

// "Tổ chức" chapter, from the owner's side of the seeded "Công ty Mẫu". Nothing is created,
// switched, removed or deactivated.
const chapter = 'organizations'
const account = '[data-testid="app-header-user-menu"]'
const owned = 'main .rounded-xl:has-text("Công ty Mẫu")'

async function openOrganizations(page: Page) {
  await page.locator(owned).waitFor()
}

for (const locale of locales) {
  test(`switch [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await page.locator(account).click()
    await page.locator('.n-popover [data-testid="organization-list-manage"]').waitFor()
    await capture.annotate([
      { selector: account, n: 1, at: 'below' },
      { selector: '.n-popover button:has-text("Công ty Mẫu")', n: 2, at: 'after' },
      { selector: '[data-testid="organization-list-manage"]', n: 3, at: 'top-right' },
    ])
    await capture.blur([account, '.n-popover [role="menu"] > div:first-child'])
    await capture.shot({ locale, chapter, name: 'switch' })
  })

  test(`list [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openOrganizations(page)
    await capture.annotate([
      { selector: 'main h1 + button', n: 1, at: 'below' },
      { selector: `${owned} .n-tag >> nth=0`, n: 2, at: 'after' },
      {
        selector: `${owned} button:has-text("${locale === 'vi' ? 'Chuyển' : 'Switch'}")`,
        n: 3,
        at: 'below',
      },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'list' })
  })

  test(`create [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await page.locator('main h1 + button').click()
    const form = '.n-dialog form'
    await page.locator(form).waitFor()
    await page
      .locator(`${form} input >> nth=0`)
      .fill(locale === 'vi' ? 'Phòng Kinh doanh' : 'Sales')
    await page
      .locator(`${form} input >> nth=1`)
      .fill(locale === 'vi' ? 'phong-kinh-doanh' : 'sales')
    await capture.annotate([
      { selector: `${form} .n-form-item >> nth=0`, n: 1, at: 'top-right' },
      { selector: `${form} .n-form-item >> nth=1`, n: 2, at: 'top-right' },
      { selector: '.n-dialog__action button >> nth=1', n: 3, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'create' })
  })
}
