import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import AmountKeypad from './AmountKeypad.vue'

test.each([
  ['加', '12+'],
  ['乘', '12*'],
  ['减', '12-'],
  ['除', '12/']
])('enters %s on the first tap', async (label, expression) => {
  const view = mount(AmountKeypad, { props: { modelValue: '12', disabled: false } })

  await view.get(`button[aria-label="${label}"]`).trigger('click')

  expect(view.emitted('update:modelValue')).toEqual([[expression]])
  view.unmount()
})
