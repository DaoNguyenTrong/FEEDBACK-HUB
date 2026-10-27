import { describe, expect, it, beforeEach, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { reloadToLogin } from '@/lib/session-exit'
import { server } from '../../helpers/msw/server'
import { useAuthStore } from '@/stores/auth'
import { hasSessionHint } from '@/lib/auth-session'
import { setupTestPinia } from '../../helpers/pinia'
import { makeAuthResponse } from '../../helpers/msw/fixtures'

vi.mock('@/lib/session-exit', () => ({ reloadTo: vi.fn(), reloadToLogin: vi.fn() }))

describe('useAuthStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('starts unauthenticated', () => {
    const auth = useAuthStore()

    expect(auth.account).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('setAuth authenticates and stores the account', () => {
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.account?.username).toBe('test')
  })

  it('clearAuth resets to initial state', () => {
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())
    auth.clearAuth()

    expect(auth.account).toBeNull()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('setAuth leaves a session hint for the next boot, clearAuth removes it', () => {
    const auth = useAuthStore()

    auth.setAuth(makeAuthResponse())
    expect(hasSessionHint()).toBe(true)

    auth.clearAuth()
    expect(hasSessionHint()).toBe(false)
  })

  it('setAuth stores the active-org permission set', () => {
    const auth = useAuthStore()
    auth.setAuth({
      ...makeAuthResponse(),
      permissions: ['organizations.members.manage'],
    })

    expect(auth.permissions).toEqual(['organizations.members.manage'])
    expect(auth.hasPermission('organizations.members.manage')).toBe(true)
    expect(auth.hasPermission('organizations.manage')).toBe(false)
  })

  it('sets isSystemAdministrator from the login response and clears it on clearAuth', () => {
    const auth = useAuthStore()
    expect(auth.isSystemAdministrator).toBe(false)

    auth.setAuth({ ...makeAuthResponse(), isSystemAdministrator: true })
    expect(auth.isSystemAdministrator).toBe(true)

    auth.setAuth({ ...makeAuthResponse(), isSystemAdministrator: false })
    expect(auth.isSystemAdministrator).toBe(false)

    auth.setAuth({ ...makeAuthResponse(), isSystemAdministrator: true })
    auth.clearAuth()
    expect(auth.isSystemAdministrator).toBe(false)
  })

  it('clearAuth empties the permission set', () => {
    const auth = useAuthStore()
    auth.setAuth({ ...makeAuthResponse(), permissions: ['organizations.manage'] })
    auth.clearAuth()

    expect(auth.permissions).toEqual([])
    expect(auth.hasPermission('organizations.manage')).toBe(false)
  })

  it('sets the session from register-with-invitation and returns the outcome', async () => {
    let body: unknown
    server.use(
      http.post('*/api/auth/register-with-invitation', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          ...makeAuthResponse(),
          invitationOutcome: 'Joined',
          organizationId: 'org-1',
          organizationName: 'Acme',
          permissions: ['organizations.members.manage'],
        })
      }),
    )
    const auth = useAuthStore()

    const response = await auth.registerWithInvitation({
      token: 'tok',
      name: 'Jane',
      username: 'jane',
      password: 'Password123!',
    })

    expect(body).toEqual({ token: 'tok', name: 'Jane', username: 'jane', password: 'Password123!' })
    expect(response.invitationOutcome).toBe('Joined')
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.organizationId).toBe('org-1')
    expect(auth.permissions).toEqual(['organizations.members.manage'])
  })

  it('externalLogin returns whether the account was just created', async () => {
    server.use(
      http.post('*/api/auth/external/google', () =>
        HttpResponse.json({ ...makeAuthResponse(), accountCreated: true }),
      ),
    )
    const auth = useAuthStore()

    const response = await auth.externalLogin('google', { credential: 'c' })

    expect(response.accountCreated).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
  })

  describe('logout', () => {
    beforeEach(() => {
      vi.mocked(reloadToLogin).mockClear()
    })

    it('clears auth and reloads to a plain login', async () => {
      server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
      const store = useAuthStore()
      store.setAuth(makeAuthResponse())

      await store.logout()

      expect(store.isAuthenticated).toBe(false)
      expect(reloadToLogin).toHaveBeenCalledWith()
    })

    it('logs out to the login page with the given redirect', async () => {
      server.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
      const store = useAuthStore()
      store.setAuth(makeAuthResponse())

      await store.logout(undefined, { redirect: '/invite' })

      expect(store.isAuthenticated).toBe(false)
      expect(reloadToLogin).toHaveBeenCalledWith({ redirect: '/invite' })
    })

    it('still clears auth and reloads when the logout request fails', async () => {
      server.use(http.post('*/api/auth/logout', () => HttpResponse.json({}, { status: 500 })))
      const store = useAuthStore()
      store.setAuth(makeAuthResponse())

      await expect(store.logout()).rejects.toBeDefined()

      expect(store.isAuthenticated).toBe(false)
      expect(reloadToLogin).toHaveBeenCalledWith()
    })
  })
})
