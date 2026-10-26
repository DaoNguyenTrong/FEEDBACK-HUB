import { DOMWrapper, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import DeactivateOrganizationDialog from '@/components/DeactivateOrganizationDialog.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'
import { stubViewport } from '../helpers/viewport'

const t = i18n.global.t

type Props = {
  reasonRequired: boolean
  variant: 'member' | 'system-member' | 'system'
  busy: boolean
}

async function render(props: Partial<Props> = {}) {
  stubViewport(1280)
  const result = await renderComponent(DeactivateOrganizationDialog, {
    props: {
      show: true,
      organizationName: 'Acme',
      reasonRequired: false,
      variant: 'member',
      busy: false,
      ...props,
    },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return result
}

const find = (testid: string) => document.body.querySelector<HTMLElement>(`[data-testid="${testid}"]`)
const reasonBox = () => new DOMWrapper(find('deactivate-org-reason') as HTMLTextAreaElement)

async function press(testid: string) {
  await new DOMWrapper(find(testid)!).trigger('click')
  await flushPromises()
}

describe('DeactivateOrganizationDialog', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('marks the reason optional and submits null when left empty for a member', async () => {
    const { wrapper } = await render({ reasonRequired: false })

    expect(document.body.textContent).toContain(t('deactivateOrganization.title', { name: 'Acme' }))
    expect(document.body.textContent).toContain(t('deactivateOrganization.optional'))
    await press('deactivate-org-submit')

    expect(wrapper.emitted('submit')).toEqual([[null]])
    expect(find('deactivate-org-reason-error')).toBeNull()
  })

  it('requires a reason for a system administrator and does not submit without one', async () => {
    const { wrapper } = await render({ reasonRequired: true, variant: 'system-member' })

    expect(document.body.textContent).not.toContain(t('deactivateOrganization.optional'))
    await press('deactivate-org-submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(find('deactivate-org-reason-error')?.textContent).toContain(
      t('errorCodes.DeactivationReasonRequired'),
    )

    await reasonBox().setValue('  Policy breach  ')
    await press('deactivate-org-submit')
    expect(wrapper.emitted('submit')).toEqual([['Policy breach']])
  })

  it('counts characters up to 500 and stops at 500', async () => {
    const { wrapper } = await render()

    await reasonBox().setValue('a'.repeat(12))
    expect(find('deactivate-org-count')?.textContent).toContain('12/500')

    await reasonBox().setValue('b'.repeat(620))
    expect(find('deactivate-org-count')?.textContent).toContain('500/500')
    await press('deactivate-org-submit')
    expect((wrapper.emitted('submit')![0]![0] as string).length).toBe(500)
  })

  it.each([
    ['member', 'member'],
    ['system-member', 'systemMember'],
    ['system', 'system'],
  ] as const)('lists the bullets of its %s variant', async (variant, key) => {
    await render({ variant })

    const list = find('deactivate-org-bullets')!.textContent
    expect(list).toContain(t(`deactivateOrganization.${key}.widget`))
    expect(list).toContain(t(`deactivateOrganization.${key}.access`))
    expect(list).toContain(t(`deactivateOrganization.${key}.data`))
  })

  it('never offers the owner email hint', async () => {
    await render({ reasonRequired: true, variant: 'system-member' })

    expect(document.body.textContent).not.toMatch(/email/i)
    expect(document.body.textContent).not.toContain('Chủ sở hữu.')
  })

  it('does not submit twice while busy', async () => {
    const { wrapper } = await render({ busy: true })

    await press('deactivate-org-submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })
})
