// @vitest-environment jsdom
import { flushPromises, mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import CategoriesView from './CategoriesView.vue'
import { fetchCategories, reorderCategories, updateCategory } from '../api/categories'
import type { EntryCategory } from '../types/category'

vi.mock('../api/categories')
vi.mock('vue-router', () => ({ useRouter: () => ({}) }))

const categories: EntryCategory[] = [
  { id: 'fixed', name: '固定', isGroup: true, sortOrder: 0, kind: 'expense', isActive: true, systemKey: null },
  { id: 'necessary', name: '必要', isGroup: true, sortOrder: 1, kind: 'expense', isActive: true, systemKey: null },
  { id: 'rent', name: '房租', parentId: 'fixed', sortOrder: 0, kind: 'expense', isActive: true, systemKey: null },
  { id: 'dining', name: '餐饮', parentId: 'necessary', sortOrder: 0, kind: 'expense', isActive: true, systemKey: null },
  { id: 'transport', name: '交通', parentId: 'necessary', sortOrder: 1, kind: 'expense', isActive: true, systemKey: null }
]

function mountPage() {
  vi.mocked(fetchCategories).mockResolvedValue(categories)
  vi.mocked(reorderCategories).mockResolvedValue()
  vi.mocked(updateCategory).mockImplementation(async (id, input) => ({
    ...categories.find(category => category.id === id)!,
    ...input
  }))
  return mount(CategoriesView, { attachTo: document.body, global: { stubs: { Teleport: true } } })
}

async function pointer(element: Element, type: string, x: number, y: number) {
  const event = new MouseEvent(type, { bubbles: true, button: 0, clientX: x, clientY: y })
  Object.defineProperty(event, 'pointerId', { value: 1 })
  element.dispatchEvent(event)
  await nextTick()
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.useRealTimers()
})

describe('分类管理', () => {
  it('保存所选图标，并在改名时保留图标', async () => {
    const wrapper = mountPage()
    try {
      await flushPromises()
      await wrapper.get('button[aria-label="更换 房租 的图标"]').trigger('click')
      await wrapper.get('button[aria-label="礼物"]').trigger('click')
      await flushPromises()
      expect(updateCategory).toHaveBeenLastCalledWith('rent', { name: '房租', isActive: true, iconKey: '1f381' })

      await wrapper.get('[data-category-id="rent"] .category-name').trigger('click')
      await wrapper.get('input[type="text"]').setValue('住房')
      await wrapper.get('form').trigger('submit')
      await flushPromises()
      expect(updateCategory).toHaveBeenLastCalledWith('rent', {
        name: '住房', isActive: true, parentId: undefined, iconKey: '1f381'
      })
    } finally {
      wrapper.unmount()
    }
  })

  it('长按后将二级分类插入目标卡片的指示线位置', async () => {
    const wrapper = mountPage()
    try {
      await flushPromises()
      vi.useFakeTimers()
      const handle = wrapper.get('button[aria-label="移动 房租"]')
      const target = wrapper.get('[data-category-id="transport"]')
      vi.spyOn(target.element, 'getBoundingClientRect').mockReturnValue({ top: 100, height: 44 } as DOMRect)
      vi.stubGlobal('requestAnimationFrame', () => 1)
      Object.defineProperty(document, 'elementFromPoint', { configurable: true, value: () => target.element })
      await pointer(handle.element, 'pointerdown', 40, 20)
      await vi.advanceTimersByTimeAsync(350)
      await pointer(handle.element, 'pointermove', 40, 110)
      expect(target.classes()).toContain('insert-before')
      await pointer(handle.element, 'pointerup', 40, 110)
      await flushPromises()
      expect(reorderCategories).toHaveBeenLastCalledWith('expense', ['dining', 'rent', 'transport'], {
        isGroup: false, parentId: 'necessary', movingId: 'rent', sourceParentId: 'fixed'
      })
    } finally {
      Reflect.deleteProperty(document, 'elementFromPoint')
      vi.unstubAllGlobals()
      wrapper.unmount()
    }
  })
})
