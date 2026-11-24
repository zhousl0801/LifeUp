import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { expect, test, vi } from 'vitest'
import BottomSheet from './BottomSheet.vue'

test('cancels a drag without closing, even after crossing the dismissal threshold', async () => {
  const view = mount(BottomSheet, {
    props: { open: true, title: '编辑' },
    global: { stubs: { teleport: true } }
  })
  const grabber = view.get('.sheet-grabber')
  Object.defineProperty(grabber.element, 'setPointerCapture', { value: vi.fn() })

  async function pointer(type: string, clientY: number) {
    grabber.element.dispatchEvent(new PointerEvent(type, { clientY, pointerId: 1, bubbles: true }))
    await nextTick()
  }

  await pointer('pointerdown', 20)
  await pointer('pointermove', 150)
  await pointer('pointercancel', 150)
  expect(view.emitted('close')).toBeUndefined()

  await pointer('pointerdown', 20)
  await pointer('pointermove', 150)
  await pointer('pointerup', 150)
  expect(view.emitted('close')).toHaveLength(1)
  view.unmount()
})

test('keeps the page locked until the final stacked sheet closes', async () => {
  vi.stubGlobal('scrollY', 240)
  const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
  document.body.style.overflow = 'auto'
  document.body.style.position = 'relative'
  document.body.style.top = '2px'
  document.body.style.width = '95%'
  const trigger = document.createElement('button')
  document.body.append(trigger)
  trigger.focus()
  const lower = mount(BottomSheet, { props: { open: true, title: '编辑' } })
  await nextTick()
  const lowerFocusRole = document.activeElement?.getAttribute('role')
  vi.stubGlobal('scrollY', 0)
  const upper = mount(BottomSheet, { props: { open: true, title: '放弃修改' } })
  try {
    await nextTick()
    expect(lowerFocusRole).toBe('dialog')
    expect(document.activeElement?.getAttribute('role')).toBe('dialog')
    expect(document.body.style.position).toBe('fixed')
    expect(document.body.style.top).toBe('-240px')

    await lower.setProps({ open: false })
    expect(document.body.style.overflow).toBe('hidden')
    expect(document.body.style.position).toBe('fixed')
    expect(scrollTo).not.toHaveBeenCalled()

    await upper.setProps({ open: false })
    expect(document.body.style.overflow).toBe('auto')
    expect(document.body.style.position).toBe('relative')
    expect(document.body.style.top).toBe('2px')
    expect(document.body.style.width).toBe('95%')
    expect(scrollTo).toHaveBeenCalledExactlyOnceWith({ left: 0, top: 240, behavior: 'instant' })
    expect(document.activeElement).toBe(trigger)
  } finally {
    upper.unmount()
    lower.unmount()
    trigger.remove()
    document.body.removeAttribute('style')
    scrollTo.mockRestore()
    vi.unstubAllGlobals()
  }
})

test('lifts the sheet above the virtual keyboard when visualViewport shrinks', async () => {
  const listeners = new Map<string, Set<EventListener>>()
  const visualViewport = {
    height: 844,
    offsetTop: 0,
    addEventListener(type: string, listener: EventListener) {
      const set = listeners.get(type) ?? new Set()
      set.add(listener)
      listeners.set(type, set)
    },
    removeEventListener(type: string, listener: EventListener) {
      listeners.get(type)?.delete(listener)
    }
  }
  vi.stubGlobal('innerHeight', 844)
  vi.stubGlobal('visualViewport', visualViewport)

  const view = mount(BottomSheet, {
    props: { open: true, title: '新增二级分类' },
    slots: { default: '<input type="text" />' },
    global: { stubs: { teleport: true } }
  })

  try {
    await nextTick()
    expect((view.get('.sheet-backdrop').element as HTMLElement).style.paddingBottom).toBe('')

    visualViewport.height = 520
    for (const listener of listeners.get('resize') ?? []) listener(new Event('resize'))
    await nextTick()

    expect((view.get('.sheet-backdrop').element as HTMLElement).style.paddingBottom).toBe('324px')
    expect(view.get('.sheet').classes()).toContain('keyboard-open')
    expect((view.get('.sheet').element as HTMLElement).style.getPropertyValue('--sheet-max-height')).toBe('504px')

    visualViewport.height = 844
    for (const listener of listeners.get('resize') ?? []) listener(new Event('resize'))
    await nextTick()

    expect((view.get('.sheet-backdrop').element as HTMLElement).style.paddingBottom).toBe('')
    expect(view.get('.sheet').classes()).not.toContain('keyboard-open')
  } finally {
    view.unmount()
    vi.unstubAllGlobals()
  }
})
