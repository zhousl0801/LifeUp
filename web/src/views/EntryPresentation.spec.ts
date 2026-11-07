import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import EntryView from './EntryView.vue'
import type { EntryCategory } from '../types/category'

const mocks = vi.hoisted(() => ({
  create: vi.fn(),
  push: vi.fn(),
  categories: vi.fn(),
  createCategory: vi.fn(),
  deleteCategory: vi.fn(),
  reorder: vi.fn()
}))
vi.mock('vue-router', () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push: mocks.push, replace: vi.fn() })
}))
vi.mock('../api/categories', () => ({
  fetchCategories: mocks.categories,
  createCategory: mocks.createCategory,
  deleteCategory: mocks.deleteCategory,
  reorderCategories: mocks.reorder
}))
vi.mock('../api/entries', () => ({
  createEntry: mocks.create, fetchEntry: vi.fn(), reverseEntry: vi.fn(), updateEntry: vi.fn()
}))

beforeEach(() => {
  vi.clearAllMocks()
  mocks.categories.mockResolvedValue([])
})

afterEach(() => {
  vi.useRealTimers()
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

test('Enter on a focused control does not submit an entered amount', async () => {
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()
  await view.get('.keypad button').trigger('click')
  expect(view.get('output[aria-label="输入金额"]').text()).toBe('7')

  const categoryEvent = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })
  view.get('.category-tile').element.dispatchEvent(categoryEvent)
  await flushPromises()
  expect(mocks.create).not.toHaveBeenCalled()
  expect(categoryEvent.defaultPrevented).toBe(false)
  view.unmount()
})

test('switching to income selects an income category and keeps four-per-row layout', async () => {
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()

  await view.findAll('.kind-tabs button')[1]!.trigger('click')

  const tiles = view.findAll('.category-tile')
  expect(tiles).toHaveLength(3)
  expect(tiles[0]!.text()).toBe('工资')
  expect(tiles[0]!.classes()).toContain('active')
  view.unmount()
})

test('long press then drag reorders categories', async () => {
  const categories: EntryCategory[] = [
    { id: 'c1', kind: 'expense', name: '餐饮', sortOrder: 0, isActive: true, systemKey: null },
    { id: 'c2', kind: 'expense', name: '购物', sortOrder: 1, isActive: true, systemKey: null },
    { id: 'c3', kind: 'expense', name: '日用', sortOrder: 2, isActive: true, systemKey: null }
  ]
  mocks.categories.mockResolvedValue(categories)
  mocks.reorder.mockResolvedValue(undefined)
  vi.useFakeTimers()

  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()

  const tiles = view.findAll('.category-tile')
  const first = tiles[0]!
  const third = tiles[2]!

  first.element.dispatchEvent(new MouseEvent('pointerdown', {
    bubbles: true,
    clientX: 10,
    clientY: 10
  }))
  vi.advanceTimersByTime(500)
  await flushPromises()

  expect(first.classes()).toContain('editing')
  expect(view.find('.category-hint').text()).toContain('拖动分类可调整顺序')

  Object.defineProperty(document, 'elementFromPoint', {
    configurable: true,
    value: vi.fn(() => third.element)
  })
  first.element.dispatchEvent(new MouseEvent('pointermove', {
    bubbles: true,
    clientX: 120,
    clientY: 120
  }))
  await flushPromises()

  const reordered = view.findAll('.category-tile')
  expect(reordered[0]!.find('.category-name').text()).toBe('购物')
  expect(reordered[2]!.find('.category-name').text()).toBe('餐饮')

  first.element.dispatchEvent(new MouseEvent('pointerup', {
    bubbles: true,
    clientX: 120,
    clientY: 120
  }))
  await flushPromises()

  expect(mocks.reorder).toHaveBeenCalledWith('expense', ['c2', 'c3', 'c1'])
  expect(mocks.deleteCategory).not.toHaveBeenCalled()
  view.unmount()
})

test('Escape in the category sheet preserves the entry draft', async () => {
  mocks.categories.mockResolvedValue([{ id: 'c1', kind: 'expense', name: '餐饮', sortOrder: 0, isActive: true, systemKey: null }])
  vi.useFakeTimers()
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()
  await view.get('.keypad button').trigger('click')
  view.get('.category-tile').element.dispatchEvent(new MouseEvent('pointerdown', { clientX: 10, clientY: 10, bubbles: true }))
  await vi.advanceTimersByTimeAsync(500)
  await view.get('.category-add').trigger('click')
  await flushPromises()
  expect(document.querySelector('[role="dialog"]')).not.toBeNull()
  document.querySelector('[role="dialog"]')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
  await flushPromises()
  expect(mocks.push).not.toHaveBeenCalled()
  expect(view.get('output[aria-label="输入金额"]').text()).toBe('7')
  view.unmount()
})
