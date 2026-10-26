import type { Page, BrowserContext } from '@playwright/test'
import { test, expect, login, logout, requireEnv } from './fixtures'

test.describe.configure({ mode: 'serial' })

const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

let context: BrowserContext
let page: Page

test.beforeAll(async ({ browser }) => {
  context = await browser.newContext()
  page = await context.newPage()
  await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await login(page, USER_USERNAME, USER_PASSWORD)
  await expect(page).toHaveURL(/#\/$/)
})

test.afterAll(async () => {
  await logout(page)
  await context.close()
})

test('authenticated user visiting /login is redirected to home', async () => {
  await page.goto('#/login')
  await expect(page).toHaveURL(/#\/$/)
})

test('signed-in user opening an unknown path sees Not found inside the app shell', async () => {
  await page.goto('#/no/such/page')
  await expect(page).toHaveURL(/#\/no\/such\/page$/)
  await expect(page.getByTestId('app-sidebar')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Not found' })).toBeVisible()
  await page.goto('#/')
})

test('unauthenticated visitor to a signed-in page is redirected to login', async ({ browser }) => {
  const anonContext = await browser.newContext()
  const anonPage = await anonContext.newPage()
  await anonPage.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await anonPage.goto('#/guide-auth')
  await expect(anonPage).toHaveURL(/#\/login(\?|$)/)
  await anonContext.close()
})
