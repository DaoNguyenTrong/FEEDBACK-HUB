import { describe, expect, it, vi } from 'vitest'
import AccessProblemState from '@/components/AccessProblemState.vue'
import i18n from '@/plugins/i18n'
import { renderComponent } from '../helpers/render'

const t = i18n.global.t

describe('AccessProblemState', () => {
  it('shows the not-found title, line and a single home button', async () => {
    const { wrapper } = await renderComponent(AccessProblemState, { props: { kind: 'not-found' } })

    expect(wrapper.get('h1').text()).toBe(t('accessProblem.notFoundTitle'))
    expect(wrapper.text()).toBe(
      [t('accessProblem.notFoundTitle'), t('accessProblem.notFoundText'), t('accessProblem.goHome')].join(''),
    )
    expect(wrapper.findAll('button')).toHaveLength(1)
  })

  it('shows the forbidden title and line with the same layout', async () => {
    const { wrapper } = await renderComponent(AccessProblemState, { props: { kind: 'forbidden' } })

    expect(wrapper.get('h1').text()).toBe(t('accessProblem.forbiddenTitle'))
    expect(wrapper.text()).toBe(
      [t('accessProblem.forbiddenTitle'), t('accessProblem.forbiddenText'), t('accessProblem.goHome')].join(''),
    )
    expect(wrapper.findAll('button')).toHaveLength(1)
  })

  it('goes to the home route from its button', async () => {
    const { wrapper, router } = await renderComponent(AccessProblemState, {
      props: { kind: 'forbidden' },
      initialRoute: '/organizations',
    })

    await wrapper.get('button').trigger('click')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
  })

  it('ignores caller attributes so a title or description cannot replace the generic text', async () => {
    const { wrapper } = await renderComponent(AccessProblemState, {
      props: { kind: 'not-found' },
      attrs: { title: 'Sao Mai Tech', description: 'Role Trưởng IT', 'data-x': '1' },
    })

    expect(wrapper.text()).toBe(
      [t('accessProblem.notFoundTitle'), t('accessProblem.notFoundText'), t('accessProblem.goHome')].join(''),
    )
    expect(wrapper.html()).not.toContain('Sao Mai Tech')
    expect(wrapper.html()).not.toContain('Trưởng IT')
    expect(wrapper.html()).not.toContain('data-x')
  })
})
