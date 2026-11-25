import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { expect, test } from 'vitest'
import MoneyAmount from '../components/MoneyAmount.vue'
import { useHomePreferencesStore } from '../stores/homePreferences'

test('MoneyAmount automatically follows global privacy masking when Pinia is active', async () => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const prefs = useHomePreferencesStore()
  expect(prefs.amountsMasked).toBe(true)

  const view = mount(MoneyAmount, {
    props: { value: 12345.67 },
    global: { plugins: [pinia] }
  })

  expect(view.text()).toBe('¥••••')

  prefs.amountsMasked = false
  await view.vm.$nextTick()
  expect(view.text()).toBe('¥12,345.67')

  prefs.amountsMasked = true
  await view.vm.$nextTick()
  expect(view.text()).toBe('¥••••')

  view.unmount()
})
