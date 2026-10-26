import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ProblemDetails } from '@/api/types'
import { KEEP_LOGIN_STORAGE_KEY } from '@/lib/auth-session'
import { reloadTo, reloadToLogin } from '@/lib/session-exit'
import { useAuthStore } from '@/stores/auth'
import { useOrganizationContextStore } from '@/stores/organization-context'
import { makeOrganization } from '../../factories/organization'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const NOTICE_KEY = 'organization-suspended-notice'

const suspended: ProblemDetails = {
  status: 403,
  title: 'Forbidden',
  code: 'OrganizationSuspended',
  organizationName: 'Acme',
}
const accessDenied: ProblemDetails = {
  status: 403,
  title: 'Forbidden',
  code: 'OrganizationAccessDenied',
}

const acme = makeOrganization({ id: 'org-1', name: 'Acme' })
const frozenAcme = makeOrganization({ id: 'org-1', name: 'Acme', status: false })

let switchBodies: unknown[]
let listCalls: number

function mockMine(organizations: unknown[] | 'fail') {
  server.use(
    http.get('*/api/organizations', () => {
      listCalls += 1
      return organizations === 'fail'
        ? HttpResponse.json({}, { status: 500 })
        : HttpResponse.json(organizations)
    }),
  )
}

function mockSwitch(status = 200) {
  server.use(
    http.post('*/api/auth/switch-organization', async ({ request }) => {
      switchBodies.push(await request.json())
      return status === 200
        ? HttpResponse.json(makeAuthResponse())
        : new HttpResponse(null, { status })
    }),
  )
}

function signIn() {
  const auth = useAuthStore()
  auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
  return auth
}

describe('organization-context store', () => {
  beforeEach(() => {
    sessionStorage.clear()
    switchBodies = []
    listCalls = 0
    vi.mocked(reloadTo).mockClear()
    vi.mocked(reloadToLogin).mockClear()
    setupTestPinia()
    signIn()
  })

  it('stores the suspended name and switches to the personal context on OrganizationSuspended', async () => {
    mockSwitch()
    const store = useOrganizationContextStore()

    await store.handleAccessProblem(suspended)

    expect(sessionStorage.getItem(NOTICE_KEY)).toBe('Acme')
    expect(switchBodies).toEqual([{ organizationId: null }])
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
  })

  it('switches without a notice when the organization is gone from my list', async () => {
    mockMine([makeOrganization({ id: 'org-2', name: 'Globex' })])
    mockSwitch()
    const store = useOrganizationContextStore()

    await store.handleAccessProblem(accessDenied)

    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
    expect(switchBodies).toEqual([{ organizationId: null }])
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
  })

  it('treats a listed but suspended organization like OrganizationSuspended', async () => {
    mockMine([frozenAcme])
    mockSwitch()
    const store = useOrganizationContextStore()

    await store.handleAccessProblem(accessDenied)

    expect(sessionStorage.getItem(NOTICE_KEY)).toBe('Acme')
    expect(switchBodies).toEqual([{ organizationId: null }])
    expect(reloadTo).toHaveBeenCalledWith('/welcome')
  })

  it('does nothing when the organization is still listed and active', async () => {
    mockMine([acme])
    mockSwitch()
    const store = useOrganizationContextStore()

    await store.handleAccessProblem(accessDenied)

    expect(switchBodies).toEqual([])
    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('stays put when the list cannot be read after an OrganizationAccessDenied', async () => {
    mockMine('fail')
    mockSwitch()
    const store = useOrganizationContextStore()

    await store.handleAccessProblem(accessDenied)

    expect(switchBodies).toEqual([])
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('reads the suspended notice once', () => {
    sessionStorage.setItem(NOTICE_KEY, 'Acme')
    const store = useOrganizationContextStore()

    expect(store.consumeSuspendedNotice()).toBe('Acme')
    expect(store.consumeSuspendedNotice()).toBeNull()
    expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
  })

  it('handles only the first problem of a page', async () => {
    mockSwitch()
    const store = useOrganizationContextStore()

    await Promise.all([store.handleAccessProblem(suspended), store.handleAccessProblem(suspended)])
    await store.handleAccessProblem(suspended)

    expect(switchBodies).toHaveLength(1)
  })

  describe('handleOrganizationLost', () => {
    it('reads my list fresh and shows the suspended notice for a listed suspended organization', async () => {
      mockMine([frozenAcme])
      mockSwitch()
      useOrganizationsStoreLoaded()
      const store = useOrganizationContextStore()

      await store.handleOrganizationLost('org-1')

      expect(listCalls).toBe(1)
      expect(sessionStorage.getItem(NOTICE_KEY)).toBe('Acme')
      expect(switchBodies).toEqual([{ organizationId: null }])
      expect(reloadTo).toHaveBeenCalledWith('/welcome')
    })

    it('goes to the welcome page without a notice when I was removed', async () => {
      mockMine([])
      mockSwitch()
      const store = useOrganizationContextStore()

      await store.handleOrganizationLost('org-1')

      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(reloadTo).toHaveBeenCalledWith('/welcome')
    })

    it('still leaves without a notice when the list cannot be read', async () => {
      mockMine('fail')
      mockSwitch()
      const store = useOrganizationContextStore()

      await store.handleOrganizationLost('org-1')

      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(reloadTo).toHaveBeenCalledWith('/welcome')
    })
  })

  describe('a lapsed session during the exit', () => {
    it('signs out and leaves no suspended notice behind', async () => {
      localStorage.setItem(KEEP_LOGIN_STORAGE_KEY, 'true')
      server.use(
        http.post('*/api/auth/switch-organization', () => new HttpResponse(null, { status: 401 })),
        http.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })),
      )
      const auth = useAuthStore()
      const store = useOrganizationContextStore()

      await store.handleAccessProblem(suspended)

      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
      expect(auth.account).toBeNull()
      expect(reloadToLogin).toHaveBeenCalledOnce()
      expect(reloadTo).not.toHaveBeenCalled()
    })

    it('drops the notice when the switch fails for another reason', async () => {
      server.use(
        http.post('*/api/auth/switch-organization', () => HttpResponse.json({}, { status: 500 })),
      )
      const store = useOrganizationContextStore()

      await store.handleAccessProblem(suspended)

      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(reloadTo).not.toHaveBeenCalled()
    })
  })

  describe('beginOwnExit', () => {
    it('makes both handlers a no-op for an exit the user started', async () => {
      mockMine([frozenAcme])
      mockSwitch()
      const store = useOrganizationContextStore()

      store.beginOwnExit()
      await store.handleOrganizationLost('org-1')
      await store.handleAccessProblem(suspended)

      expect(switchBodies).toEqual([])
      expect(listCalls).toBe(0)
      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(reloadTo).not.toHaveBeenCalled()
    })

    it('lets the handlers act again after the exit is cancelled', async () => {
      mockSwitch()
      const store = useOrganizationContextStore()

      store.beginOwnExit()
      store.cancelOwnExit()
      await store.handleAccessProblem(suspended)

      expect(switchBodies).toHaveLength(1)
    })

    it('lets a later problem through when the own exit started during the list fetch', async () => {
      const store = useOrganizationContextStore()
      server.use(
        http.get('*/api/organizations', () => {
          store.beginOwnExit()
          return HttpResponse.json([acme])
        }),
      )

      await store.handleAccessProblem(accessDenied)
      store.cancelOwnExit()
      mockSwitch()
      await store.handleAccessProblem(suspended)

      expect(switchBodies).toEqual([{ organizationId: null }])
    })

    it('clears the own exit when leaveToWelcome fails', async () => {
      mockSwitch(500)
      const store = useOrganizationContextStore()

      store.beginOwnExit()
      await expect(store.leaveToWelcome()).rejects.toBeDefined()
      mockSwitch()
      await store.handleAccessProblem(suspended)

      expect(switchBodies).toHaveLength(2)
    })

    it('still lets leaveToWelcome switch to the personal context without a notice', async () => {
      mockSwitch()
      const store = useOrganizationContextStore()

      store.beginOwnExit()
      await store.leaveToWelcome()

      expect(switchBodies).toEqual([{ organizationId: null }])
      expect(sessionStorage.getItem(NOTICE_KEY)).toBeNull()
      expect(reloadTo).toHaveBeenCalledWith('/welcome')
    })
  })
})

function useOrganizationsStoreLoaded() {
  const organizations = useOrganizationsStore()
  organizations.loaded = true
  organizations.myOrganizations = [acme]
}
