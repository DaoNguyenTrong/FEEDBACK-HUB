import type { Page, BrowserContext } from '@playwright/test'
import { test, expect, loginInto, logout, switchTo, requireEnv } from './fixtures'

const ADMIN_USERNAME = requireEnv('E2E_ADMIN_USERNAME')
const ADMIN_PASSWORD = requireEnv('E2E_ADMIN_PASSWORD')
const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

test.describe('owner of a new organization', () => {
  test.describe.configure({ mode: 'serial' })

  const stamp = Date.now()
  const orgName = `E2E Org ${stamp}`
  let context: BrowserContext
  let page: Page

  test.beforeAll(async ({ browser }) => {
    context = await browser.newContext()
    page = await context.newPage()
    await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
    await loginInto(page, ADMIN_USERNAME, ADMIN_PASSWORD)
  })

  test.afterAll(async () => {
    // An account with two active organizations signs in to the welcome page instead of home, which breaks
    // the other specs. An organization cannot be deleted, so the new one is deactivated (as its owner).
    const trigger = page.getByTestId('org-switcher-trigger')
    if ((await trigger.isVisible()) && (await trigger.innerText()).includes(orgName)) {
      await page.goto('#/organization/settings')
      await page.getByTestId('org-settings-deactivate-open').click()
      const reloaded = page.waitForEvent('load')
      await page.getByTestId('deactivate-org-submit').click()
      await reloaded
      await expect(page.getByTestId('welcome-greeting')).toBeVisible()
    }
    await logout(page)
    await context.close()
  })

  // AC-56, AC-281
  test('creates an organization with the suggested identifier and lands inside it', async () => {
    await page.getByTestId('org-switcher-trigger').click()
    await page.getByTestId('org-switcher-create').click()

    await page.getByTestId('create-org-name').fill(orgName)
    await expect(page.getByTestId('create-org-slug')).toHaveValue(`e2e-org-${stamp}`)

    const reloaded = page.waitForEvent('load')
    await page.getByTestId('create-org-submit').click()
    await reloaded

    await expect(page.getByTestId('org-switcher-trigger')).toContainText(orgName)
  })

  // AC-04
  test('switches organizations from the top-bar switcher', async () => {
    await switchTo(page, 'Dev')
    await switchTo(page, orgName)
  })

  // AC-277
  test('the only owner of a new organization cannot leave it', async () => {
    await page.goto('#/organization/members')
    await page.getByTestId('member-leave').click()
    await page.getByTestId('inline-confirm-confirm').click()

    await expect(
      page.getByText(
        'You are the only owner. Give the owner role to someone else before you leave.',
      ),
    ).toBeVisible()
    await expect(page.getByTestId('inline-confirm-confirm')).toHaveCount(0)
    await expect(page.getByTestId('org-switcher-trigger')).toContainText(orgName)
  })
})

test.describe('a member of Dev', () => {
  test.describe.configure({ mode: 'serial' })

  let context: BrowserContext
  let page: Page

  test.beforeAll(async ({ browser }) => {
    context = await browser.newContext()
    page = await context.newPage()
    await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
    await loginInto(page, USER_USERNAME, USER_PASSWORD)
  })

  test.afterAll(async () => {
    await logout(page)
    await context.close()
  })

  // AC-298
  test('a member sees no Invitations, Roles or Settings and opening them shows No permission', async () => {
    await page.goto('#/organization/members')
    await expect(page.getByTestId('tab-members')).toBeVisible()
    await expect(page.getByTestId('tab-invitations')).toHaveCount(0)
    await expect(page.getByTestId('tab-roles')).toHaveCount(0)
    await expect(page.getByTestId('invite-member-btn')).toHaveCount(0)
    await expect(
      page.getByTestId('app-sidebar').getByRole('link', { name: 'Organization management' }),
    ).toHaveCount(0)

    for (const path of [
      '#/organization/members/invitations',
      '#/organization/members/roles',
      '#/organization/settings',
    ]) {
      await page.goto(path)
      await expect(page).toHaveURL(new RegExp(`${path}$`))
      await expect(page.getByRole('heading', { name: 'No permission' })).toBeVisible()
    }
  })
})
