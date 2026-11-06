import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import LoadingButton from './LoadingButton.vue'

test('loading button disables itself and exposes a spinner and busy label', () => {
  const view = mount(LoadingButton, {
    props: { loading: true, loadingLabel: '处理中…' },
    slots: { default: '保存' }
  })
  const button = view.get('button')

  expect(button.attributes('disabled')).toBeDefined()
  expect(button.attributes('aria-busy')).toBe('true')
  expect(button.text()).toBe('处理中…')
  expect(button.find('.loading-button-spinner').exists()).toBe(true)
  expect(button.text()).not.toContain('保存')
})

test('loading button renders its label and stays enabled when idle', () => {
  const view = mount(LoadingButton, {
    slots: { default: '保存' }
  })
  const button = view.get('button')

  expect(button.attributes('disabled')).toBeUndefined()
  expect(button.attributes('aria-busy')).toBeUndefined()
  expect(button.text()).toBe('保存')
  expect(button.find('.loading-button-spinner').exists()).toBe(false)
})
