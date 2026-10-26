import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { Building, Home } from '@vicons/tabler'
import AppRailNav from '@/layouts/AppRailNav.vue'
import i18n from '@/plugins/i18n'

const t = i18n.global.t
const stub = { template: '<div />' }

async function mountNav(variant: 'wide' | 'icon', initialPath = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        component: stub,
        children: [
          {
            path: '',
            name: 'home',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.home', icon: Home, order: 1 } },
          },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: {
              sidebar: { labelKey: 'nav.organizations', icon: Building, group: 'organization' },
            },
          },
        ],
      },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  const wrapper = mount(AppRailNav, {
    props: { variant },
    global: {
      plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false }), router],
    },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('AppRailNav', () => {
  it('heads each group with its name in the wide variant only', async () => {
    const wide = await mountNav('wide')
    expect(wide.wrapper.text()).toContain(t('nav.groups.organization'))

    const icon = await mountNav('icon')
    expect(icon.wrapper.text()).not.toContain(t('nav.groups.organization'))
  })

  it('emits navigate when the current link is clicked', async () => {
    const { wrapper } = await mountNav('wide', '/')

    await wrapper.get('a[aria-current="page"]').trigger('click')

    expect(wrapper.emitted('navigate')).toHaveLength(1)
  })
})
