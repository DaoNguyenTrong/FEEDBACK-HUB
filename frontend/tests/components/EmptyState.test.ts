import { describe, expect, it } from 'vitest'
import EmptyState from '@/components/EmptyState.vue'
import { renderComponent } from '../helpers/render'

describe('EmptyState', () => {
  it('renders the title and the description', async () => {
    const { wrapper } = await renderComponent(EmptyState, {
      props: { title: 'Nothing here', description: 'Create the first one.' },
    })
    expect(wrapper.get('h2').text()).toBe('Nothing here')
    expect(wrapper.text()).toContain('Create the first one.')
  })

  it('omits the description when none is given', async () => {
    const { wrapper } = await renderComponent(EmptyState, { props: { title: 'Nothing here' } })
    expect(wrapper.find('p').exists()).toBe(false)
  })

  it('renders the icon and action slots', async () => {
    const { wrapper } = await renderComponent(EmptyState, {
      props: { title: 'Nothing here' },
      slots: { icon: '<svg data-testid="icon" />', action: '<button>Add</button>' },
    })
    expect(wrapper.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.find('button').text()).toBe('Add')
  })

  it('renders the title as an h1 when asked to', async () => {
    const { wrapper } = await renderComponent(EmptyState, {
      props: { title: 'Nothing here', headingLevel: 1 },
    })
    expect(wrapper.get('h1').text()).toBe('Nothing here')
    expect(wrapper.find('h2').exists()).toBe(false)
  })
})
