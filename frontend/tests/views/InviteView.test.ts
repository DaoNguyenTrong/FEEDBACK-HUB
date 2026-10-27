import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InviteView from '@/views/InviteView.vue'
import i18n from '@/plugins/i18n'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'
import { formatDateTime } from '@/lib/date-format'
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from '@/lib/password-policy'
import type { InvitationLookupDto } from '@/api/types'
import { makeInvitationLookup } from '../factories/organization'
import { makeAccount, makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const t = i18n.global.t
const TOKEN_KEY = 'invitation-token'
const NOTICE_KEY = 'organization-suspended-notice'
const EXPIRES_AT = '2026-11-01T00:00:00Z'

function problem(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

interface RenderOptions {
  route?: string
  lookup?: Partial<InvitationLookupDto>
  signedIn?: boolean
}

// `lookups` records, per request, the address-bar hash seen at the moment the lookup was sent.
async function render({ route = '/invite?token=tok-1', lookup, signedIn }: RenderOptions = {}) {
  const lookups: { token: unknown; hash: string }[] = []
  server.use(
    http.post('*/api/invitations/lookup', async ({ request }) => {
      const body = (await request.json()) as { token: unknown }
      lookups.push({ token: body.token, hash: window.location.hash })
      return HttpResponse.json(makeInvitationLookup({ expiresAt: EXPIRES_AT, ...lookup }))
    }),
  )
  window.history.replaceState(null, '', `#${route}`)
  const result = await renderComponent(InviteView, {
    initialRoute: route,
    initialState: signedIn
      ? { auth: { account: makeAccount({ email: 'new@example.com' }), organizationId: null } }
      : undefined,
    attachTo: document.body,
  })
  await flushPromises()
  return { ...result, lookups }
}

async function fillAndSubmit(wrapper: Awaited<ReturnType<typeof render>>['wrapper']) {
  await wrapper.get('input#invite-name').setValue('Jane Doe')
  await wrapper.get('input#invite-username').setValue('jane')
  await wrapper.get('input#invite-password').setValue('Password123!')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

function joinedResponse(overrides: Record<string, unknown> = {}) {
  return {
    ...makeAuthResponse({ email: 'new@example.com' }),
    invitationOutcome: 'Joined',
    organizationId: 'org-1',
    organizationName: 'Acme Inc',
    ...overrides,
  }
}

describe('InviteView', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(reloadTo).mockClear()
    vi.mocked(reloadToLogin).mockClear()
    // Explicitly empty: a developer's real frontend/.env would otherwise configure the providers.
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '')
    vi.stubEnv('VITE_MICROSOFT_CLIENT_ID', '')
  })
  afterEach(() => {
    document.body.innerHTML = ''
    document.head.querySelectorAll('meta[name]').forEach((meta) => meta.remove())
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
    delete window.google
  })

  it('removes the token from the address bar before calling the lookup', async () => {
    const { lookups } = await render()

    expect(lookups).toHaveLength(1)
    expect(lookups[0]?.token).toBe('tok-1')
    expect(lookups[0]?.hash).not.toContain('tok-1')
    expect(window.location.hash).not.toContain('token')
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe('tok-1')
  })

  it('keeps using the token from sessionStorage after a reload or a login round trip', async () => {
    sessionStorage.setItem(TOKEN_KEY, 'tok-stored')

    const { lookups } = await render({ route: '/invite' })

    expect(lookups.map((lookup) => lookup.token)).toEqual(['tok-stored'])
  })

  it('adds noindex and no-referrer meta tags while open and removes them after', async () => {
    const { wrapper } = await render()

    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
    expect(document.head.querySelector('meta[name="referrer"]')?.getAttribute('content')).toBe(
      'no-referrer',
    )

    wrapper.unmount()

    expect(document.head.querySelector('meta[name="robots"]')).toBeNull()
    expect(document.head.querySelector('meta[name="referrer"]')).toBeNull()
  })

  it('shows only organization, role, expiry and the read-only email to a visitor', async () => {
    const { wrapper } = await render()

    const text = wrapper.text()
    expect(text).toContain(t('invite.title', { org: 'Acme Inc' }))
    expect(text).toContain(
      t('invite.summary', { role: 'Member', time: formatDateTime(EXPIRES_AT, 'en') }),
    )
    const email = wrapper.get('input#invite-email')
    expect((email.element as HTMLInputElement).value).toBe('new@example.com')
    expect(email.attributes('readonly')).toBeDefined()
    expect(text).toContain(t('invite.emailHint'))
    expect(text).toContain(
      t('invite.passwordHint', { min: PASSWORD_MIN_LENGTH, max: PASSWORD_MAX_LENGTH }),
    )
    expect(wrapper.find('[data-testid="invite-accept"]').exists()).toBe(false)
    const link = wrapper.get('[data-testid="invite-login-link"]')
    expect(link.attributes('href')).toBe('/login?redirect=/invite')
  })

  it('registers, then reloads into the organization when the outcome is Joined', async () => {
    let body: Record<string, unknown> = {}
    server.use(
      http.post('*/api/auth/register-with-invitation', async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(joinedResponse())
      }),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    expect(body).toEqual({
      token: 'tok-1',
      name: 'Jane Doe',
      username: 'jane',
      password: 'Password123!',
    })
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it('registers, then goes to welcome with the suspended notice when the organization is suspended', async () => {
    server.use(
      http.post('*/api/auth/register-with-invitation', () =>
        HttpResponse.json(
          joinedResponse({
            invitationOutcome: 'OrganizationSuspended',
            organizationId: null,
            organizationName: null,
          }),
        ),
      ),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    expect(sessionStorage.getItem(NOTICE_KEY)).toBe('Acme Inc')
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
  })

  it('validates the form without calling the API', async () => {
    let calls = 0
    server.use(
      http.post('*/api/auth/register-with-invitation', () => {
        calls += 1
        return HttpResponse.json(joinedResponse())
      }),
    )
    const { wrapper } = await render()

    await wrapper.get('input#invite-password').setValue('short')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain(t('auth.nameRequired'))
    expect(wrapper.text()).toContain(t('auth.usernameRequired'))
    expect(wrapper.text()).toContain(t('auth.passwordTooShort', { min: PASSWORD_MIN_LENGTH }))
    expect(calls).toBe(0)
  })

  it('shows a taken username under the username field', async () => {
    server.use(
      http.post('*/api/auth/register-with-invitation', () =>
        problem({ status: 409, title: 'Conflict', code: 'AccountUsernameAlreadyExists' }, 409),
      ),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    expect(wrapper.text()).toContain(t('auth.usernameAlreadyExists'))
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('tells a visitor whose email already has an account to sign in', async () => {
    server.use(
      http.post('*/api/auth/register-with-invitation', () =>
        problem({ status: 409, title: 'Conflict', code: 'AccountEmailAlreadyExists' }, 409),
      ),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    const notice = wrapper.get('[data-testid="invite-email-taken"]')
    expect(notice.text()).toContain(t('invite.emailTaken'))
    expect(notice.get('a').attributes('href')).toBe('/login?redirect=/invite')
  })

  it('shows the too-many-attempts notice on 429', async () => {
    server.use(
      http.post('*/api/auth/register-with-invitation', () =>
        problem({ status: 429, title: 'Too Many Requests' }, 429),
      ),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    expect(wrapper.get('[data-testid="invite-too-many"]').text()).toContain(
      t('errors.tooManyRequests'),
    )
  })

  it('moves to the expired state when registering finds the invitation expired', async () => {
    server.use(
      http.post('*/api/auth/register-with-invitation', () =>
        problem({ status: 409, title: 'Conflict', code: 'InvitationExpired' }, 409),
      ),
    )
    const { wrapper } = await render()

    await fillAndSubmit(wrapper)

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.expired.title'),
    )
  })

  it('accepts for a signed-in invitee and switches into the organization', async () => {
    let accepted: unknown
    let switched: unknown
    server.use(
      http.post('*/api/invitations/accept', async ({ request }) => {
        accepted = await request.json()
        return HttpResponse.json({ organizationId: 'org-1', organizationName: 'Acme Inc' })
      }),
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        switched = await request.json()
        return HttpResponse.json(joinedResponse())
      }),
    )
    const { wrapper } = await render({ signedIn: true })

    const card = wrapper.get('[data-testid="invite-account-card"]')
    expect(card.text()).toContain('Test User')
    expect(card.text()).toContain('new@example.com')
    expect(wrapper.find('form').exists()).toBe(false)

    await wrapper.get('[data-testid="invite-accept"]').trigger('click')
    await flushPromises()

    expect(accepted).toEqual({ token: 'tok-1' })
    expect(switched).toEqual({ organizationId: 'org-1' })
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it.each([
    ['InvitationWrongAccount', 409, 'invite.wrongAccount.title'],
    ['InvitationExpired', 409, 'invite.expired.title'],
    ['InvitationNoLongerValid', 409, 'invite.noLongerValid.title'],
    ['EmailNotConfirmed', 403, 'invite.emailNotConfirmed.title'],
  ])('moves to the matching state when accepting fails with %s', async (code, status, title) => {
    server.use(
      http.post('*/api/invitations/accept', () =>
        problem({ status, title: 'Refused', code }, status),
      ),
    )
    const { wrapper } = await render({ signedIn: true })

    await wrapper.get('[data-testid="invite-accept"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(t(title))
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('moves to the suspended state, with the expiry, when accepting finds the organization suspended', async () => {
    server.use(
      http.post('*/api/invitations/accept', () =>
        problem({ status: 409, title: 'Conflict', code: 'InvitationOrganizationSuspended' }, 409),
      ),
    )
    const { wrapper } = await render({ signedIn: true })

    await wrapper.get('[data-testid="invite-accept"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.suspended.title', { org: 'Acme Inc' }),
    )
  })

  describe('after an external sign-in', () => {
    async function renderWithGoogle() {
      vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client-id')
      let credentialCallback: (response: { credential: string }) => void = () => undefined
      vi.stubGlobal('google', {
        accounts: {
          id: {
            initialize: (config: { callback: typeof credentialCallback }) => {
              credentialCallback = config.callback
            },
            prompt: vi.fn(),
          },
        },
      })
      const rendered = await render()
      return {
        ...rendered,
        signInWithGoogle: async () => {
          credentialCallback({ credential: 'google-credential' })
          await flushPromises()
        },
      }
    }

    it('accepts right after an external sign-in that created the account', async () => {
      let accepted = 0
      let switched: unknown
      server.use(
        http.post('*/api/auth/external/google', () =>
          HttpResponse.json({ ...makeAuthResponse({ email: 'new@example.com' }), accountCreated: true }),
        ),
        http.post('*/api/invitations/accept', () => {
          accepted += 1
          return HttpResponse.json({ organizationId: 'org-1', organizationName: 'Acme Inc' })
        }),
        http.post('*/api/auth/switch-organization', async ({ request }) => {
          switched = await request.json()
          return HttpResponse.json(joinedResponse())
        }),
      )
      const { signInWithGoogle } = await renderWithGoogle()

      await signInWithGoogle()

      expect(accepted).toBe(1)
      expect(switched).toEqual({ organizationId: 'org-1' })
      expect(reloadTo).toHaveBeenCalledWith('/')
    })

    it('looks the invitation up again and offers to accept when the account already existed', async () => {
      let accepted = 0
      server.use(
        http.post('*/api/auth/external/google', () =>
          HttpResponse.json({
            ...makeAuthResponse({ email: 'new@example.com' }),
            accountCreated: false,
          }),
        ),
        http.post('*/api/invitations/accept', () => {
          accepted += 1
          return HttpResponse.json({ organizationId: 'org-1', organizationName: 'Acme Inc' })
        }),
      )
      const { wrapper, signInWithGoogle, lookups } = await renderWithGoogle()

      await signInWithGoogle()

      expect(lookups).toHaveLength(2)
      expect(accepted).toBe(0)
      expect(wrapper.find('[data-testid="invite-account-card"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="invite-accept"]').exists()).toBe(true)
    })
  })

  it('shows only the wrong-account message and a sign-out button', async () => {
    server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
    const { wrapper } = await render({
      signedIn: true,
      lookup: { status: 'WrongAccount', organizationName: null, role: null, expiresAt: null, email: null },
    })

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.wrongAccount.title'),
    )
    expect(wrapper.find('[data-testid="invite-account-card"]').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('new@example.com')
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe('tok-1')

    await wrapper.get('[data-testid="invite-signout"]').trigger('click')
    await flushPromises()

    expect(reloadToLogin).toHaveBeenCalledWith({ redirect: '/invite' })
  })

  it('shows the expired message without saying wrong account', async () => {
    const { wrapper } = await render({
      signedIn: true,
      lookup: { status: 'Expired', organizationName: null, role: null, email: null },
    })

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.expired.title'),
    )
    expect(wrapper.text()).toContain(
      t('invite.expired.text', { time: formatDateTime(EXPIRES_AT, 'en') }),
    )
    expect(wrapper.text()).not.toContain(t('invite.wrongAccount.title'))
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull()
  })

  it('shows the verify-email message with a resend button', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/resend-verification', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await render({
      signedIn: true,
      lookup: { status: 'EmailNotConfirmed', organizationName: null, role: null, expiresAt: null, email: null },
    })

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.emailNotConfirmed.title'),
    )
    expect(wrapper.text()).toContain(t('invite.emailNotConfirmed.text'))

    await wrapper.get('[data-testid="invite-resend"]').trigger('click')
    await flushPromises()

    expect(body).toEqual({ email: 'new@example.com' })
    expect(wrapper.text()).toContain(t('auth.resendVerificationSuccess'))
  })

  it('shows the no-longer-valid message when the token is missing', async () => {
    const { wrapper, lookups } = await render({ route: '/invite' })

    expect(lookups).toHaveLength(0)
    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.noLongerValid.title'),
    )
  })

  it('shows the no-longer-valid message when the token is unknown, and forgets it', async () => {
    const { wrapper } = await render({
      lookup: { status: 'NoLongerValid', organizationName: null, role: null, expiresAt: null, email: null },
    })

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.noLongerValid.title'),
    )
    expect(wrapper.text()).toContain(t('invite.noLongerValid.text'))
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull()
  })

  it('shows the suspended message with the expiry', async () => {
    const { wrapper } = await render({ lookup: { status: 'OrganizationSuspended', role: null, email: null } })

    expect(wrapper.get('[data-testid="invite-state-title"]').text()).toBe(
      t('invite.suspended.title', { org: 'Acme Inc' }),
    )
    expect(wrapper.text()).toContain(
      t('invite.suspended.text', { time: formatDateTime(EXPIRES_AT, 'en') }),
    )
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe('tok-1')
  })

  it('shows an error, not a state, when the lookup itself fails', async () => {
    server.use(http.post('*/api/invitations/lookup', () => HttpResponse.json({}, { status: 500 })))
    window.history.replaceState(null, '', '#/invite?token=tok-1')
    const { wrapper } = await renderComponent(InviteView, {
      initialRoute: '/invite?token=tok-1',
      attachTo: document.body,
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="invite-state-title"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="invite-load-error"]').text()).not.toBe('')
  })

  it('renders an organization name containing HTML as text', async () => {
    const name = '<img src=x onerror=alert(1)>Acme'
    const { wrapper } = await render({ lookup: { organizationName: name } })

    expect(wrapper.find('img[src="x"]').exists()).toBe(false)
    expect(wrapper.text()).toContain(name)
  })
})
