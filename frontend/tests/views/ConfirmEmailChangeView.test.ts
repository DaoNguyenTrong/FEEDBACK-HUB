import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import ConfirmEmailChangeView from '@/views/ConfirmEmailChangeView.vue'
import { server } from '../helpers/msw/server'
import { storeIn } from '../helpers/pinia'
import { renderComponent } from '../helpers/render'
import en from '@/locales/en'

function problemResponse(code: string, status: number) {
  return HttpResponse.json(
    { status, title: 'Err', code, detail: 'backend text' },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

const accountState = {
  auth: {
    account: {
      id: 'a1',
      name: 'Jane',
      username: 'jane',
      email: 'old@example.com',
      emailConfirmed: true,
      status: true,
      phone: null,
      position: null,
      address: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: null,
    },
  },
}

describe('ConfirmEmailChangeView', () => {
  it('removes the token from the address bar before calling the API', async () => {
    let tokenInQueryAtRequest: unknown = 'not-called'
    let routerSeen = false
    let body: Record<string, unknown> | undefined
    let routerRef: Awaited<ReturnType<typeof renderComponent>>['router'] | undefined
    server.use(
      http.post('*/api/auth/confirm-email-change', async ({ request }) => {
        routerSeen = routerRef !== undefined
        tokenInQueryAtRequest = routerRef?.currentRoute.value.query.token
        body = (await request.json()) as Record<string, unknown>
        return new HttpResponse(null, { status: 204 })
      }),
    )

    // The request fires after mount, a macrotask later than the render promise resolving, so the
    // handler sees the router assigned below; `routerSeen` guards against a vacuous pass.
    const result = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc123',
    })
    routerRef = result.router
    await flushPromises()

    expect(routerSeen).toBe(true)
    expect(body?.token).toBe('abc123')
    expect(tokenInQueryAtRequest).toBeUndefined()
    expect(result.router.currentRoute.value.query.token).toBeUndefined()
  })

  it('shows success on 204', async () => {
    server.use(
      http.post('*/api/auth/confirm-email-change', () => new HttpResponse(null, { status: 204 })),
    )

    const { wrapper } = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc',
    })
    await flushPromises()

    expect(wrapper.text()).toContain(en.confirmEmailChange.success)
  })

  it('shows noLongerValid on EmailChangeLinkNoLongerValid', async () => {
    server.use(
      http.post('*/api/auth/confirm-email-change', () =>
        problemResponse('EmailChangeLinkNoLongerValid', 409),
      ),
    )

    const { wrapper } = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc',
    })
    await flushPromises()

    expect(wrapper.text()).toContain(en.confirmEmailChange.noLongerValid)
  })

  it('shows alreadyTaken on AccountEmailAlreadyExists', async () => {
    server.use(
      http.post('*/api/auth/confirm-email-change', () =>
        problemResponse('AccountEmailAlreadyExists', 409),
      ),
    )

    const { wrapper } = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc',
    })
    await flushPromises()

    expect(wrapper.text()).toContain(en.confirmEmailChange.alreadyTaken)
  })

  it('shows a missing-token error and makes no API call without a token', async () => {
    let called = false
    server.use(
      http.post('*/api/auth/confirm-email-change', () => {
        called = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change',
    })
    await flushPromises()

    expect(wrapper.text()).toContain(en.confirmEmailChange.missingToken)
    expect(called).toBe(false)
  })

  it('reloads the profile when signed in', async () => {
    let profileCalls = 0
    server.use(
      http.post('*/api/auth/confirm-email-change', () => new HttpResponse(null, { status: 204 })),
      http.get('*/api/profile', () => {
        profileCalls += 1
        return HttpResponse.json({
          id: 'a1',
          name: 'Jane',
          username: 'jane',
          email: 'new@example.com',
          emailConfirmed: true,
          hasPassword: true,
        })
      }),
    )

    const { pinia } = await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc',
      initialState: accountState,
    })
    await flushPromises()

    expect(profileCalls).toBe(1)
    expect(storeIn(pinia, useAuthStore).account?.email).toBe('new@example.com')
  })

  it('does not read the profile when signed out', async () => {
    let profileCalls = 0
    server.use(
      http.post('*/api/auth/confirm-email-change', () => new HttpResponse(null, { status: 204 })),
      http.get('*/api/profile', () => {
        profileCalls += 1
        return new HttpResponse(null, { status: 401 })
      }),
    )

    await renderComponent(ConfirmEmailChangeView, {
      initialRoute: '/confirm-email-change?token=abc',
    })
    await flushPromises()

    expect(profileCalls).toBe(0)
  })
})
