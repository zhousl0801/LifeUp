import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import EntryView from './EntryView.vue'
import type { EntryCategory } from '../types/category'

const mocks = vi.hoisted(() => ({
  create: vi.fn(),
  push: vi.fn(),
  replace: vi.fn(),
  categories: vi.fn(),
  createCategory: vi.fn(),
  deleteCategory: vi.fn(),
  reorder: vi.fn()
}))
vi.mock('vue-router', () => ({
  onBeforeRouteLeave: vi.fn(),
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push: mocks.push, replace: mocks.replace, options: { history: { state: {} } } })
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

test('shows distinct color artwork for group, leaf and custom categories', async () => {
  mocks.categories.mockResolvedValue([
    { id: 'g1', kind: 'expense', name: '必要', sortOrder: 0, isActive: true, isGroup: true, systemKey: null },
    { id: 'g2', kind: 'expense', name: '自定义组', sortOrder: 1, isActive: true, isGroup: true, systemKey: null },
    { id: 'c1', kind: 'expense', name: '餐饮', sortOrder: 0, isActive: true, parentId: 'g1', systemKey: null },
    { id: 'c2', kind: 'expense', name: '自定义项', sortOrder: 1, isActive: true, parentId: 'g1', systemKey: null }
  ] satisfies EntryCategory[])

  const view = mount(EntryView)
  await flushPromises()
  await view.get('.category-groups button').trigger('click')

  const groups = view.findAll('.category-groups img')
  const leaves = view.findAll('.category-tile img')
  expect(groups).toHaveLength(2)
  expect(leaves).toHaveLength(2)
  const icons = [...groups, ...leaves]
  expect(new Set(icons.map(icon => icon.attributes('src'))).size).toBe(4)
  expect(icons.every(icon => icon.attributes('alt') === '')).toBe(true)
  view.unmount()
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

test('switching to income requires a category and restores the selected expense category', async () => {
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()

  await view.get('.category-tile').trigger('click')
  await view.findAll('.kind-tabs button')[1]!.trigger('click')

  const tiles = view.findAll('.category-tile')
  expect(tiles).toHaveLength(3)
  expect(tiles[0]!.text()).toBe('工资')
  expect(tiles[0]!.classes()).not.toContain('active')
  await view.findAll('.kind-tabs button')[0]!.trigger('click')
  expect(view.get('.category-tile').classes()).toContain('active')
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

test('requires explicit category selection and opens the saved business date', async () => {
  mocks.create.mockResolvedValue({ entry: { id: 'past-entry', businessDate: '2026-10-31' } })
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()
  await view.get('.keypad button').trigger('click')
  expect(view.find('[aria-pressed="true"].category-tile').exists()).toBe(false)
  await view.get('.category-tile').trigger('click')
  await view.get('input[type="date"]').setValue('2026-10-31')
  await view.get('button.entry-save').trigger('click')
  await flushPromises()

  expect(mocks.create).toHaveBeenCalledWith(expect.objectContaining({
    businessDate: '2026-10-31', amount: 7, category: '餐饮'
  }))
  expect(mocks.replace).toHaveBeenCalledWith({
    name: 'ledger', query: { date: '2026-10-31', focus: 'past-entry' }
  })
  view.unmount()
})

test('save and add another keeps the batch date, clears the draft and uses a fresh request ID', async () => {
  mocks.create.mockResolvedValue({ entry: { id: 'saved', businessDate: '2026-11-07' } })
  const view = mount(EntryView, { attachTo: document.body })
  await flushPromises()

  const date = view.get<HTMLInputElement>('input[type="date"]')
  const note = view.get<HTMLInputElement>('input[aria-label="备注"]')
  await date.setValue('2026-11-07')
  await note.setValue('第一笔')
  await view.get('.category-tile').trigger('click')
  await view.get('.keypad button').trigger('click')
  await view.get('button.entry-save-again').trigger('click')
  await flushPromises()

  expect(mocks.replace).not.toHaveBeenCalled()
  expect(view.get('[role="status"]').text()).toBe('已保存，可以记下一笔。')
  expect(view.get('output').text()).toBe('0')
  expect(note.element.value).toBe('')
  expect(date.element.value).toBe('2026-11-07')
  expect(view.get('.entry-date').text()).toBe('2026年11月7日 星期六')
  expect(view.find('.category-tile[aria-pressed="true"]').exists()).toBe(false)

  await view.get('.category-tile').trigger('click')
  await view.get('.keypad button').trigger('click')
  await view.get('button.entry-save').trigger('click')
  await flushPromises()

  expect(mocks.create).toHaveBeenCalledTimes(2)
  expect(mocks.create.mock.calls[0]![0].requestId).not.toBe(mocks.create.mock.calls[1]![0].requestId)
  expect(mocks.create.mock.calls[1]![0]).toMatchObject({ businessDate: '2026-11-07', note: '' })
  view.unmount()
})
