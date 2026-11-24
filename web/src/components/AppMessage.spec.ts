import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import AppMessage from './AppMessage.vue'

test('uses the shared top gap and lets Sonner apply the mobile safe area', () => {
  const wrapper = mount(AppMessage)
  try {
    const { style } = wrapper.get<HTMLOListElement>('[data-sonner-toaster]').element

    expect(style.getPropertyValue('--offset-top')).toBe('var(--app-safe-top)')
    expect(style.getPropertyValue('--mobile-offset-top')).toBe('var(--app-top-gap)')
  } finally {
    wrapper.unmount()
  }
})
