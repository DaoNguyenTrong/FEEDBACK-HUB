import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/auth'
import { useOrganizationContextStore } from '@/stores/organization-context'
import { KEEP_LOGIN_STORAGE_KEY, setKeepLoginPreference } from '@/lib/auth-session'
import { server } from '../../helpers/msw/server'
import { makeAuthResponse } from '../../helpers/msw/fixtures'
import { setupTestPinia } from '../../helpers/pinia'

const exits = vi.hoisted(() => ({ reloadTo: vi.fn(), reloadToLogin: vi.fn() }))

vi.mock('@/lib/session-exit', () => exits)

function isApiError(error: unknown): error is { name: string; status: number } {
  return (
    typeof error === 'object' &&
    error !== null &&
    (error as { name?: string }).name === 'ApiError' &&
    typeof (error as { status?: number }).status === 'number'
  )
}

async function loadApiClient() {
  const { apiClient } = await import('@/api/client')
  return apiClient
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((res) => {
    resolve = res
  })
  return { promise, resolve }
}

describe('apiClient interceptors', () => {
  beforeEach(() => {
    vi.resetModules()
    exits.reloadTo.mockClear()
    exits.reloadToLogin.mockClear()
    setupTestPinia()
  })

  it('returns successful responses', async () => {
    server.use(http.get('*/api/test-protected', () => HttpResponse.json({ ok: true })))

    const apiClient = await loadApiClient()
    const response = await apiClient.get('/api/test-protected')

    expect(response.data).toEqual({ ok: true })
  })

  it('sends array query params as repeated keys, which ASP.NET binds', async () => {
    let search = ''
    server.use(
      http.get('*/api/test-array', ({ request }) => {
        search = new URL(request.url).search
        return HttpResponse.json({ ok: true })
      }),
    )

    const apiClient = await loadApiClient()
    await apiClient.get('/api/test-array', { params: { ids: [3, 4] } })

    expect(search).toBe('?ids=3&ids=4')
  })

  it('refreshes on 401 then retries the original request', async () => {
    let protectedCalls = 0

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        if (protectedCalls === 1) {
          return HttpResponse.json(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
    )

    const apiClient = await loadApiClient()
    const response = await apiClient.get('/api/test-protected')
    const auth = useAuthStore()

    expect(response.data).toEqual({ ok: true })
    expect(protectedCalls).toBe(2)
    expect(auth.account?.username).toBe('test')
  })

  it('queues a second concurrent 401 instead of triggering a second refresh', async () => {
    let protectedCalls = 0
    let refreshCalls = 0
    const refreshGate = deferred<void>()

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        // The first 2 calls are the two original (pre-refresh) requests;
        // everything after is a post-refresh retry.
        if (protectedCalls <= 2) {
          return HttpResponse.json(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', async () => {
        refreshCalls++
        await refreshGate.promise
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    const first = apiClient.get('/api/test-protected')
    // Wait until the first 401 has started the refresh, so the second request deterministically
    // hits the isRefreshing branch instead of racing to start its own refresh.
    await vi.waitFor(() => {
      if (refreshCalls !== 1) throw new Error('refresh not started yet')
    })

    const second = apiClient.get('/api/test-protected')
    // Axios sends request 2 regardless of isRefreshing and only queues it once its 401 lands; let
    // the refresh resolve before that and its original and retry interleave on the call counter.
    await vi.waitFor(() => {
      if (protectedCalls !== 2) throw new Error('second original 401 not received yet')
    })

    refreshGate.resolve()

    const [firstResponse, secondResponse] = await Promise.all([first, second])

    expect(refreshCalls).toBe(1)
    expect(firstResponse.data).toEqual({ ok: true })
    expect(secondResponse.data).toEqual({ ok: true })
  })

  it('rejects every queued request when the shared refresh fails', async () => {
    let protectedCalls = 0
    let refreshCalls = 0
    const refreshGate = deferred<void>()

    server.use(
      http.get('*/api/test-protected', () => {
        protectedCalls++
        return HttpResponse.json(null, { status: 401 })
      }),
      http.post('*/api/auth/refresh', async () => {
        refreshCalls++
        await refreshGate.promise
        return HttpResponse.json(null, { status: 401 })
      }),
    )

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    const first = apiClient.get('/api/test-protected')
    await vi.waitFor(() => {
      if (refreshCalls !== 1) throw new Error('refresh not started yet')
    })

    const second = apiClient.get('/api/test-protected')
    // Same reasoning as the success-path test above: wait for request 2's
    // own 401 round-trip before letting the shared refresh (and thus both
    // requests' terminal rejection) proceed.
    await vi.waitFor(() => {
      if (protectedCalls !== 2) throw new Error('second original 401 not received yet')
    })

    refreshGate.resolve()

    await expect(first).rejects.toBeDefined()
    await expect(second).rejects.toBeDefined()
    expect(refreshCalls).toBe(1)
    expect(auth.account).toBeNull()
  })

  it('clears auth when refresh fails', async () => {
    server.use(
      http.get('*/api/test-protected', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })),
    )

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    await expect(apiClient.get('/api/test-protected')).rejects.toBeDefined()
    expect(auth.account).toBeNull()
  })

  it('clears the keep-login preference when a mid-session refresh fails', async () => {
    server.use(
      http.get('*/api/test-protected', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => HttpResponse.json(null, { status: 401 })),
    )
    setKeepLoginPreference(true)

    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    await expect(apiClient.get('/api/test-protected')).rejects.toBeDefined()
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).toBeNull()
  })

  it('keeps the session when the replay after a successful refresh fails', async () => {
    let calls = 0
    server.use(
      http.get('*/api/test-protected', () =>
        ++calls === 1
          ? HttpResponse.json(null, { status: 401 })
          : HttpResponse.json(
              { status: 404, title: 'Not Found' },
              { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
            ),
      ),
      http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
    )
    setKeepLoginPreference(true)
    const apiClient = await loadApiClient()
    const auth = useAuthStore()
    auth.setAuth(makeAuthResponse())

    await expect(apiClient.get('/api/test-protected')).rejects.toMatchObject({ status: 404 })

    expect(auth.account).not.toBeNull()
    expect(auth.clearAuth).not.toHaveBeenCalled()
    expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).not.toBeNull()
  })

  it('settles a 401 that arrives while the first replay is still in flight', async () => {
    const refreshGate = deferred<void>()
    const lateGate = deferred<void>()
    const replayGate = deferred<void>()
    let firstCalls = 0
    let refreshCalls = 0
    let lateCalls = 0
    server.use(
      http.get('*/api/test-first', async () => {
        if (++firstCalls === 1) return HttpResponse.json(null, { status: 401 })
        await replayGate.promise
        return HttpResponse.json({ ok: 'first' })
      }),
      http.get('*/api/test-late', async () => {
        if (++lateCalls === 1) {
          await lateGate.promise
          return HttpResponse.json(null, { status: 401 })
        }
        return HttpResponse.json({ ok: 'late' })
      }),
      http.post('*/api/auth/refresh', async () => {
        if (++refreshCalls === 1) await refreshGate.promise
        return HttpResponse.json(makeAuthResponse())
      }),
    )
    const apiClient = await loadApiClient()
    useAuthStore().setAuth(makeAuthResponse())

    const first = apiClient.get('/api/test-first')
    await vi.waitFor(() => {
      if (refreshCalls !== 1) throw new Error('refresh not started yet')
    })
    const late = apiClient.get('/api/test-late')
    await vi.waitFor(() => {
      if (lateCalls !== 1) throw new Error('late request not sent yet')
    })
    refreshGate.resolve()
    await vi.waitFor(() => {
      if (firstCalls !== 2) throw new Error('first replay not sent yet')
    })
    lateGate.resolve()

    await expect(late).resolves.toMatchObject({ data: { ok: 'late' } })
    replayGate.resolve()
    await expect(first).resolves.toMatchObject({ data: { ok: 'first' } })
    expect(refreshCalls).toBe(2)
  })

  it('does not refresh on auth flow 401', async () => {
    let refreshCalls = 0

    server.use(
      http.post('*/api/auth/login', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    await expect(apiClient.post('/api/auth/login', {})).rejects.toBeDefined()
    expect(refreshCalls).toBe(0)
  })

  it('refreshes and retries switch-organization after a bodyless 401', async () => {
    let switchCalls = 0
    let refreshCalls = 0

    server.use(
      http.post('*/api/auth/switch-organization', () => {
        switchCalls++
        return switchCalls === 1
          ? new HttpResponse(null, { status: 401 })
          : HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    const response = await apiClient.post('/api/auth/switch-organization', {})
    expect(response.data).toEqual({ ok: true })
    expect(refreshCalls).toBe(1)
    expect(switchCalls).toBe(2)
  })

  it.each([
    ['get', '/api/auth/sessions'],
    ['delete', '/api/auth/sessions/42'],
    ['post', '/api/auth/sessions/revoke-others'],
  ] as const)('refreshes and retries %s %s after a bodyless 401', async (method, url) => {
    let calls = 0
    let refreshCalls = 0

    server.use(
      http[method](`*${url}`, () => {
        calls++
        return calls === 1 ? new HttpResponse(null, { status: 401 }) : HttpResponse.json({ ok: true })
      }),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    const response = await apiClient.request({ method, url })
    expect(response.data).toEqual({ ok: true })
    expect(refreshCalls).toBe(1)
    expect(calls).toBe(2)
  })

  it('does not refresh on a logout 401', async () => {
    let refreshCalls = 0

    server.use(
      http.post('*/api/auth/logout', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    await expect(apiClient.post('/api/auth/logout', {})).rejects.toBeDefined()
    expect(refreshCalls).toBe(0)
  })

  it('does not refresh on password change 401', async () => {
    let refreshCalls = 0

    server.use(
      http.put('*/api/profile/password', () => HttpResponse.json(null, { status: 401 })),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()

    await expect(
      apiClient.put('/api/profile/password', { currentPassword: 'a', newPassword: 'b' }),
    ).rejects.toBeDefined()
    expect(refreshCalls).toBe(0)
  })

  it.each([
    ['InvalidCurrentPassword', '/api/profile'],
    ['EmailChangeTemporarilyLocked', '/api/profile'],
    ['PasswordChangeTemporarilyLocked', '/api/profile/password'],
  ] as const)('keeps the session and surfaces the error on a %s 401', async (code, url) => {
    let refreshCalls = 0
    let calls = 0

    server.use(
      http.put(`*${url}`, () => {
        calls++
        return HttpResponse.json(
          { status: 401, title: 'Unauthorized', code },
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
        )
      }),
      http.post('*/api/auth/refresh', () => {
        refreshCalls++
        return HttpResponse.json(makeAuthResponse())
      }),
    )

    const apiClient = await loadApiClient()
    const auth = useAuthStore()

    await expect(apiClient.put(url, { email: 'a@b.c' })).rejects.toMatchObject({
      name: 'ApiError',
      status: 401,
      problem: { code },
    })
    expect(refreshCalls).toBe(0)
    expect(calls).toBe(1)
    expect(auth.clearAuth).not.toHaveBeenCalled()
  })

  it('maps 429 responses to ApiError', async () => {
    server.use(
      http.get('*/api/test-protected', () =>
        HttpResponse.json(
          { status: 429, title: 'Too Many Requests' },
          {
            status: 429,
            headers: { 'Content-Type': 'application/problem+json' },
          },
        ),
      ),
    )

    const apiClient = await loadApiClient()

    try {
      await apiClient.get('/api/test-protected')
      expect.unreachable('expected request to fail')
    } catch (error) {
      expect(isApiError(error)).toBe(true)
      if (isApiError(error)) {
        expect(error.status).toBe(429)
      }
    }
  })

  describe('organization access (TECH00 6.5)', () => {
    function problem(code: string, extra: Record<string, unknown> = {}) {
      return HttpResponse.json(
        { status: 403, title: 'Forbidden', code, ...extra },
        { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
      )
    }

    it('reports 403 OrganizationSuspended with its organizationName to the handler', async () => {
      server.use(
        http.get('*/api/organization/members', () =>
          problem('OrganizationSuspended', { organizationName: 'Acme' }),
        ),
      )
      const handler = vi.fn()
      const { apiClient, setOrganizationAccessHandler } = await import('@/api/client')
      setOrganizationAccessHandler(handler)

      await expect(apiClient.get('/api/organization/members')).rejects.toMatchObject({
        status: 403,
        problem: { code: 'OrganizationSuspended', organizationName: 'Acme' },
      })

      expect(handler).toHaveBeenCalledOnce()
      expect(handler.mock.calls[0]?.[0]).toMatchObject({
        code: 'OrganizationSuspended',
        organizationName: 'Acme',
      })
      expect(handler.mock.calls[0]?.[1]).toBe('/api/organization/members')
    })

    it('reports 403 OrganizationAccessDenied', async () => {
      server.use(http.get('*/api/organization/roles', () => problem('OrganizationAccessDenied')))
      const handler = vi.fn()
      const { apiClient, setOrganizationAccessHandler } = await import('@/api/client')
      setOrganizationAccessHandler(handler)

      await expect(apiClient.get('/api/organization/roles')).rejects.toMatchObject({ status: 403 })

      expect(handler).toHaveBeenCalledOnce()
    })

    it('does not report a 403 with another code', async () => {
      server.use(
        http.get('*/api/organization/roles', () => problem('InsufficientRolePermissions')),
      )
      const handler = vi.fn()
      const { apiClient, setOrganizationAccessHandler } = await import('@/api/client')
      setOrganizationAccessHandler(handler)

      await expect(apiClient.get('/api/organization/roles')).rejects.toMatchObject({ status: 403 })

      expect(handler).not.toHaveBeenCalled()
    })

    it.each([
      '/api/organizations',
      '/api/organizations/abc',
      '/api/auth/switch-organization',
      '/api/invitations/lookup',
      '/api/system/organizations?page=1',
      '/api/permissions',
    ])('does not report a 403 from the exempt path %s', async (url) => {
      server.use(http.all('*/api/*', () => problem('OrganizationSuspended')))
      const handler = vi.fn()
      const { apiClient, setOrganizationAccessHandler } = await import('@/api/client')
      setOrganizationAccessHandler(handler)

      await expect(apiClient.get(url)).rejects.toMatchObject({ status: 403 })

      expect(handler).not.toHaveBeenCalled()
    })

    it('does not replay requests when a refresh drops the organization', async () => {
      let membersCalls = 0
      let refreshCalls = 0
      const refreshGate = deferred<void>()
      server.use(
        http.get('*/api/organization/members', () => {
          membersCalls++
          return HttpResponse.json(null, { status: 401 })
        }),
        http.post('*/api/auth/refresh', async () => {
          refreshCalls++
          await refreshGate.promise
          return HttpResponse.json(makeAuthResponse())
        }),
      )
      setKeepLoginPreference(true)
      const auth = useAuthStore()
      auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
      const context = useOrganizationContextStore()
      vi.mocked(context.handleOrganizationLost).mockResolvedValue()
      const apiClient = await loadApiClient()

      const first = apiClient.get('/api/organization/members')
      await vi.waitFor(() => {
        if (refreshCalls !== 1) throw new Error('refresh not started yet')
      })
      const second = apiClient.get('/api/organization/members')
      await vi.waitFor(() => {
        if (membersCalls !== 2) throw new Error('second original 401 not received yet')
      })
      refreshGate.resolve()

      const results = await Promise.allSettled([first, second])

      expect(results.map((result) => result.status)).toEqual(['rejected', 'rejected'])
      expect(membersCalls).toBe(2)
      expect(refreshCalls).toBe(1)
      expect(auth.organizationId).toBeNull()
      expect(auth.account).not.toBeNull()
      expect(localStorage.getItem(KEEP_LOGIN_STORAGE_KEY)).not.toBeNull()
      const [one, two] = results as PromiseRejectedResult[]
      expect(one!.reason).not.toBe(two!.reason)
      expect(context.handleOrganizationLost).toHaveBeenCalledOnce()
      expect(context.handleOrganizationLost).toHaveBeenCalledWith('org-1')
    })

    it('replays a request that does not depend on the organization when the refresh drops it', async () => {
      let calls = 0
      server.use(
        http.post('*/api/auth/switch-organization', () =>
          ++calls === 1 ? new HttpResponse(null, { status: 401 }) : HttpResponse.json({ ok: 1 }),
        ),
        http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
      )
      const auth = useAuthStore()
      auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
      const context = useOrganizationContextStore()
      const apiClient = await loadApiClient()

      const response = await apiClient.post('/api/auth/switch-organization', {
        organizationId: 'org-2',
      })

      expect(response.data).toEqual({ ok: 1 })
      expect(calls).toBe(2)
      expect(auth.account).not.toBeNull()
      expect(context.handleOrganizationLost).not.toHaveBeenCalled()
    })

    it('does not replay a request sent in the organization that a later refresh already dropped', async () => {
      const lateGate = deferred<void>()
      let lateCalls = 0
      let firstCalls = 0
      server.use(
        http.get('*/api/organization/first', () => {
          firstCalls++
          return HttpResponse.json(null, { status: 401 })
        }),
        http.get('*/api/organization/late', async () => {
          lateCalls++
          await lateGate.promise
          return HttpResponse.json(null, { status: 401 })
        }),
        http.post('*/api/auth/refresh', () => HttpResponse.json(makeAuthResponse())),
      )
      const auth = useAuthStore()
      auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
      const context = useOrganizationContextStore()
      vi.mocked(context.handleOrganizationLost).mockResolvedValue()
      const apiClient = await loadApiClient()

      const late = apiClient.get('/api/organization/late')
      await vi.waitFor(() => {
        if (lateCalls !== 1) throw new Error('late request not sent yet')
      })
      await expect(apiClient.get('/api/organization/first')).rejects.toBeDefined()
      expect(auth.organizationId).toBeNull()
      vi.mocked(context.handleOrganizationLost).mockClear()
      lateGate.resolve()

      await expect(late).rejects.toBeDefined()

      expect(lateCalls).toBe(1)
      expect(firstCalls).toBe(1)
      expect(context.handleOrganizationLost).toHaveBeenCalledWith('org-1')
    })

    it('reloads instead of replaying when the refresh returns another organization', async () => {
      const refreshGate = deferred<void>()
      let membersCalls = 0
      let refreshCalls = 0
      server.use(
        http.get('*/api/organization/members', () => {
          membersCalls++
          return HttpResponse.json(null, { status: 401 })
        }),
        http.post('*/api/auth/refresh', async () => {
          refreshCalls++
          await refreshGate.promise
          return HttpResponse.json({
            ...makeAuthResponse(),
            organizationId: 'org-2',
            organizationName: 'Globex',
          })
        }),
      )
      const auth = useAuthStore()
      auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
      const context = useOrganizationContextStore()
      const apiClient = await loadApiClient()

      const first = apiClient.get('/api/organization/members')
      await vi.waitFor(() => {
        if (refreshCalls !== 1) throw new Error('refresh not started yet')
      })
      const second = apiClient.get('/api/organization/members')
      await vi.waitFor(() => {
        if (membersCalls !== 2) throw new Error('second original 401 not received yet')
      })
      refreshGate.resolve()

      const results = await Promise.allSettled([first, second])

      expect(results.map((result) => result.status)).toEqual(['rejected', 'rejected'])
      expect(membersCalls).toBe(2)
      expect(exits.reloadTo).toHaveBeenCalledExactlyOnceWith('/')
      expect(context.handleOrganizationLost).not.toHaveBeenCalled()
    })

    it('still replays when the refresh keeps the organization', async () => {
      let calls = 0
      server.use(
        http.get('*/api/organization/members', () =>
          ++calls === 1 ? HttpResponse.json(null, { status: 401 }) : HttpResponse.json({ ok: 1 }),
        ),
        http.post('*/api/auth/refresh', () =>
          HttpResponse.json({
            ...makeAuthResponse(),
            organizationId: 'org-1',
            organizationName: 'Acme',
          }),
        ),
      )
      const auth = useAuthStore()
      auth.setAuth({ ...makeAuthResponse(), organizationId: 'org-1', organizationName: 'Acme' })
      const context = useOrganizationContextStore()
      const apiClient = await loadApiClient()

      const response = await apiClient.get('/api/organization/members')

      expect(response.data).toEqual({ ok: 1 })
      expect(context.handleOrganizationLost).not.toHaveBeenCalled()
    })
  })
})
