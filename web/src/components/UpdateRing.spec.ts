import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import UpdateRing from './UpdateRing.vue'

test('renders determinate progress with accessible values', () => {
  const ring = mount(UpdateRing, { props: { value: 1, total: 8 } })

  expect(ring.attributes('role')).toBe('progressbar')
  expect(ring.attributes('aria-valuenow')).toBe('1')
  expect(ring.attributes('aria-valuemax')).toBe('8')
  expect(ring.attributes('aria-valuetext')).toBe('1/8')
  expect(ring.findAll('circle')).toHaveLength(2)
})

test('renders an indeterminate ring while updating', () => {
  const ring = mount(UpdateRing, { props: { value: 1, total: 8, indeterminate: true } })

  expect(ring.classes()).toContain('is-indeterminate')
  expect(ring.attributes('aria-valuetext')).toBe('正在更新')
  expect(ring.attributes('aria-valuenow')).toBeUndefined()
})

test('clamps progress to the available range', () => {
  const full = mount(UpdateRing, { props: { value: 99, total: 8 } })
  const empty = mount(UpdateRing, { props: { value: -3, total: 0 } })

  expect(full.attributes('aria-valuenow')).toBe('8')
  expect(empty.attributes('aria-valuenow')).toBe('0')
})
