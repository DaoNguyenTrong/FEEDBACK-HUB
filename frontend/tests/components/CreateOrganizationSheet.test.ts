import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import CreateOrganizationSheet from '@/components/CreateOrganizationSheet.vue'
import i18n from '@/plugins/i18n'
import { reloadTo } from '@/lib/session-exit'
import { makeOrganization } from '../factories/organization'
import { makeAccount, makeAuthResponse } from '../helpers/msw/fixtures'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import { stubViewport } from '../helpers/viewport'

vi.mock('@/lib/session-exit', () => ({
  reloadTo: vi.fn(),
  reloadToLogin: vi.fn(),
}))

const t = i18n.global.t

async function render() {
  stubViewport(1280)
  const result = await renderComponent(CreateOrganizationSheet, {
    props: { show: true },
    initialState: { auth: { account: makeAccount({ email: 'owner@example.com' }) } },
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

type Wrapper = Awaited<ReturnType<typeof render>>['wrapper']

const nameInput = (w: Wrapper) => w.get<HTMLInputElement>('[data-testid="create-org-name"]')
const slugInput = (w: Wrapper) => w.get<HTMLInputElement>('[data-testid="create-org-slug"]')

async function submit(wrapper: Wrapper) {
  await wrapper.get('[data-testid="create-org-submit"]').trigger('click')
  await flushPromises()
}

function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('CreateOrganizationSheet', () => {
  beforeEach(() => {
    vi.mocked(reloadTo).mockClear()
  })

  it('suggests the identifier from the name until the identifier is edited', async () => {
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Công ty Đông Á')
    expect(slugInput(wrapper).element.value).toBe('cong-ty-dong-a')

    await slugInput(wrapper).setValue('Dong-A')
    expect(slugInput(wrapper).element.value).toBe('dong-a')

    await nameInput(wrapper).setValue('Công ty Hải Đăng')
    expect(slugInput(wrapper).element.value).toBe('dong-a')
  })

  it('keeps a suggestion shorter than three characters', async () => {
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('AB')

    expect(slugInput(wrapper).element.value).toBe('ab')
  })

  it('tells that the identifier cannot be changed after creation', async () => {
    const { wrapper } = await render()

    expect(wrapper.text()).toContain(t('organizations.slugHint'))
  })

  it('sends name and identifier, then switches into the new organization', async () => {
    let created: unknown
    let switched: unknown
    server.use(
      http.post('*/api/organizations', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(makeOrganization({ id: 'org-9' }), { status: 201 })
      }),
      http.post('*/api/auth/switch-organization', async ({ request }) => {
        switched = await request.json()
        return HttpResponse.json(makeAuthResponse())
      }),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Công ty Đông Á')
    await submit(wrapper)

    expect(created).toEqual({ name: 'Công ty Đông Á', slug: 'cong-ty-dong-a' })
    expect(switched).toEqual({ organizationId: 'org-9' })
    expect(reloadTo).toHaveBeenCalledWith('/')
  })

  it('shows the identifier rule under the identifier on OrganizationSlugInvalid', async () => {
    server.use(
      http.post('*/api/organizations', () =>
        problem(400, { status: 400, code: 'OrganizationSlugInvalid' }),
      ),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Sao Mai')
    await slugInput(wrapper).setValue('sao_mai')
    await submit(wrapper)

    expect(wrapper.get('[data-testid="create-org-slug-error"]').text()).toBe(
      t('errorCodes.OrganizationSlugInvalid'),
    )
    expect(reloadTo).not.toHaveBeenCalled()
  })

  it('shows that the identifier is taken without naming any organization', async () => {
    server.use(
      http.post('*/api/organizations', () =>
        problem(409, {
          status: 409,
          code: 'OrganizationSlugAlreadyExists',
          detail: 'Organization Globex already uses it',
        }),
      ),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Sao Mai')
    await submit(wrapper)

    const error = wrapper.get('[data-testid="create-org-slug-error"]')
    expect(error.text()).toBe(t('errorCodes.OrganizationSlugAlreadyExists'))
    expect(wrapper.text()).not.toContain('Globex')
  })

  it('shows server field errors under their field', async () => {
    server.use(
      http.post('*/api/organizations', () =>
        problem(400, {
          status: 400,
          code: 'ValidationFailed',
          errors: { Name: ['Name is too long'] },
        }),
      ),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Sao Mai')
    await submit(wrapper)

    expect(wrapper.get('[data-testid="create-org-name-error"]').text()).toBe('Name is too long')
  })

  it('offers to resend the verification email on EmailNotConfirmed', async () => {
    let resent: unknown
    server.use(
      http.post('*/api/organizations', () =>
        problem(403, { status: 403, code: 'EmailNotConfirmed' }),
      ),
      http.post('*/api/auth/resend-verification', async ({ request }) => {
        resent = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('Sao Mai')
    await submit(wrapper)

    const notice = wrapper.get('[data-testid="create-org-unverified"]')
    expect(notice.text()).toContain(t('errorCodes.EmailNotConfirmed'))
    await wrapper.get('[data-testid="create-org-resend"]').trigger('click')
    await flushPromises()

    expect(resent).toEqual({ email: 'owner@example.com' })
    expect(wrapper.text()).toContain(t('auth.resendVerificationSuccess'))
  })

  it('requires a name before calling the server', async () => {
    let calls = 0
    server.use(
      http.post('*/api/organizations', () => {
        calls += 1
        return HttpResponse.json(makeOrganization(), { status: 201 })
      }),
    )
    const { wrapper } = await render()

    await nameInput(wrapper).setValue('   ')
    await submit(wrapper)

    expect(calls).toBe(0)
    expect(wrapper.get('[data-testid="create-org-name-error"]').text()).toBe(
      t('organizations.nameRequired'),
    )
  })
})
