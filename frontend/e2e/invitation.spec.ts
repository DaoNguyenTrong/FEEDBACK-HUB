import type { Page, BrowserContext } from '@playwright/test'
import { test, expect, loginInto, logout, requireEnv } from './fixtures'
import { latestInvitationLink } from './mailpit'

const ADMIN_USERNAME = requireEnv('E2E_ADMIN_USERNAME')
const ADMIN_PASSWORD = requireEnv('E2E_ADMIN_PASSWORD')
const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

test.describe.configure({ mode: 'serial' })

let context: BrowserContext
let page: Page

test.beforeAll(async ({ browser }) => {
  context = await browser.newContext()
  page = await context.newPage()
  await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await loginInto(page, ADMIN_USERNAME, ADMIN_PASSWORD)
})

test.afterAll(async () => {
  await logout(page)
  await context.close()
})

// Only the fragment changes between these pages, so the app would keep the list it loaded earlier.
async function openFresh(path: string) {
  await page.goto(path)
  await page.reload()
}

async function invite(email: string) {
  await page.goto('#/organization/members')
  await page.getByTestId('invite-member-btn').click()
  await page.getByTestId('invite-email').fill(email)
  await page.getByRole('radio', { name: 'Member', exact: true }).check()
  await page.getByTestId('invite-send').click()
  await expect(page.getByText(`Invitation sent to ${email}`)).toBeVisible()
}

async function openAnonymously(browser: import('@playwright/test').Browser, link: string) {
  const anonContext = await browser.newContext()
  const anonPage = await anonContext.newPage()
  await anonPage.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await anonPage.goto(link)
  return { anonContext, anonPage }
}

// AC-57, AC-182, AC-304
test('an invitee registers from the emailed link and joins Dev as Member', async ({ browser }) => {
  const stamp = Date.now()
  const email = `e2e+${stamp}@test.local`
  const name = `E2E Invitee ${stamp}`
  await invite(email)

  const link = await latestInvitationLink(email)
  const { anonContext, anonPage } = await openAnonymously(browser, link)
  try {
    await expect(anonPage.getByTestId('invite-title')).toHaveText('Invitation to Dev')
    // The token must not stay in the address bar.
    await expect(anonPage).not.toHaveURL(/token=/)

    await expect(anonPage.locator('#invite-email')).toHaveValue(email)
    await expect(anonPage.locator('#invite-email')).not.toBeEditable()

    await anonPage.locator('#invite-name').fill(name)
    await anonPage.locator('#invite-username').fill(`e2e-invitee-${stamp}`)
    await anonPage.locator('#invite-password').fill('E2e-Invitee!2026')
    const reloaded = anonPage.waitForEvent('load')
    await anonPage.getByTestId('invite-submit').click()
    await reloaded

    await expect(anonPage.getByTestId('org-switcher-trigger')).toContainText('Dev')
  } finally {
    await anonContext.close()
  }

  // The owner sees the new member, and the audit log recorded both steps.
  await openFresh('#/organization/members')
  const row = page.locator('[data-testid^="member-row-"]').filter({ hasText: email })
  await expect(row).toBeVisible()

  await openFresh('#/organization/audit-log')
  await expect(page.getByText(`sent an invitation to ${email} (role Member)`)).toBeVisible()
  await expect(page.getByText(`(${email}) joined Dev with the role Member`)).toBeVisible()

  // Clean up: remove the member again so the next run starts from the seeded state.
  await openFresh('#/organization/members')
  await row.getByTestId('member-menu').click()
  await page.getByText('Remove from organization', { exact: true }).click()
  await page.getByTestId('inline-confirm-confirm').click()
  await expect(row).toHaveCount(0)
})

// AC-306
test('a signed-in different account sees only the wrong-account message', async ({ browser }) => {
  const email = `e2e+wrong-${Date.now()}@test.local`
  await invite(email)
  const link = await latestInvitationLink(email)

  const otherContext = await browser.newContext()
  const otherPage = await otherContext.newPage()
  await otherPage.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  try {
    await loginInto(otherPage, USER_USERNAME, USER_PASSWORD)
    await otherPage.goto(link)

    await expect(otherPage.getByTestId('invite-state-WrongAccount')).toBeVisible()
    await expect(otherPage.getByTestId('invite-state-title')).toHaveText(
      "This invitation isn't for the account you are using",
    )
    await expect(otherPage.getByTestId('invite-signout')).toBeVisible()
    await expect(otherPage.getByTestId('invite-accept')).toHaveCount(0)
    await expect(otherPage.getByTestId('invite-form')).toHaveCount(0)
    // Nothing about the invitation beyond the generic message.
    await expect(otherPage.getByText(email)).toHaveCount(0)
  } finally {
    await otherContext.close()
  }

  // Clean up: revoke the invitation nobody used.
  await openFresh('#/organization/members/invitations')
  const row = page.locator('[data-testid^="invitation-row-"]').filter({ hasText: email })
  await row.getByTestId('invitation-revoke').click()
  await page.getByTestId('inline-confirm-confirm').click()
  await expect(row).toHaveCount(0)
})
