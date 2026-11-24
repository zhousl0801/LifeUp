import { mount } from '@vue/test-utils'
import { expect, test, vi } from 'vitest'
import MoneyAmount from './MoneyAmount.vue'

test('shows the actual monetary value on the first render and after an update', async () => {
  vi.stubEnv('MODE', 'development')
  const view = mount(MoneyAmount, { props: { value: 148000.24 } })
  try {
    expect(view.text()).toBe('¥148,000.24')
    await view.setProps({ value: 162530.50 })
    expect(view.text()).toBe('¥162,530.50')
  } finally {
    view.unmount()
    vi.unstubAllEnvs()
  }
})
