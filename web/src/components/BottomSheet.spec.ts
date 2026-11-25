import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { expect, test, vi } from 'vitest'
import BottomSheet from './BottomSheet.vue'
import { useRouteMotion } from '../utils/routeMotion'

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

test('applies bounded upward rubber-band resistance and never dismisses upward', async () => {
  const view = mount(BottomSheet, {
    props: { open: true, title: '编辑' },
    global: { stubs: { teleport: true } }
  })
  const grabber = view.get('.sheet-grabber')
  Object.defineProperty(grabber.element, 'setPointerCapture', { value: vi.fn() })

  try {
    grabber.element.dispatchEvent(new PointerEvent('pointerdown', { clientY: 200, pointerId: 1, bubbles: true }))
    await nextTick()
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 40, pointerId: 1, bubbles: true }))
    await nextTick()

    const rawOffset = (view.get('.sheet').element as HTMLElement).style.getPropertyValue('--sheet-drag-y')
    const offset = Number.parseFloat(rawOffset)
    expect(Number.isFinite(offset)).toBe(true)
    expect(offset).toBeLessThan(0)
    expect(offset).toBeGreaterThan(-25)

    grabber.element.dispatchEvent(new PointerEvent('pointerup', { clientY: 40, pointerId: 1, bubbles: true }))
    await nextTick()
    expect(view.emitted('close')).toBeUndefined()
  } finally {
    view.unmount()
  }
})

test('dismisses on a fast downward flick without reversal, and cancels on closeDisabled mid-drag', async () => {
  let now = 1000
  const perfSpy = vi.spyOn(performance, 'now').mockImplementation(() => now)
  const view = mount(BottomSheet, {
    props: { open: true, title: '编辑', closeDisabled: false },
    global: { stubs: { teleport: true } }
  })
  const grabber = view.get('.sheet-grabber')
  Object.defineProperty(grabber.element, 'setPointerCapture', { value: vi.fn() })

  try {
    // Fast downward flick: 40px in 30ms => ~1.33px/ms >= 0.75px/ms
    grabber.element.dispatchEvent(new PointerEvent('pointerdown', { clientY: 100, pointerId: 1, bubbles: true }))
    now += 30
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 140, pointerId: 1, bubbles: true }))
    grabber.element.dispatchEvent(new PointerEvent('pointerup', { clientY: 140, pointerId: 1, bubbles: true }))
    await nextTick()
    expect(view.emitted('close')).toHaveLength(1)

    // Mid-drag closeDisabled clears drag state cleanly
    grabber.element.dispatchEvent(new PointerEvent('pointerdown', { clientY: 100, pointerId: 2, bubbles: true }))
    now += 20
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 160, pointerId: 2, bubbles: true }))
    await nextTick()
    expect(view.get('.sheet').classes()).toContain('dragging')

    await view.setProps({ closeDisabled: true })
    await nextTick()
    expect(view.get('.sheet').classes()).not.toContain('dragging')
    expect((view.get('.sheet').element as HTMLElement).style.getPropertyValue('--sheet-drag-y')).toBe('0px')
  } finally {
    view.unmount()
    perfSpy.mockRestore()
  }
})

test('recesses lower sheet and tracks visibleSheetCount when stacked', async () => {
  const { visibleSheetCount, hasOpenSheet } = useRouteMotion()
  const lower = mount(BottomSheet, {
    props: { open: true, title: '底层表单' },
    global: { stubs: { teleport: true } }
  })
  await nextTick()
  expect(visibleSheetCount.value).toBe(1)
  expect(hasOpenSheet.value).toBe(true)
  expect(lower.get('.sheet').classes()).not.toContain('is-recessed')

  const upper = mount(BottomSheet, {
    props: { open: true, title: '确认弹窗' },
    global: { stubs: { teleport: true } }
  })
  await nextTick()
  expect(visibleSheetCount.value).toBe(2)
  expect(lower.get('.sheet').classes()).toContain('is-recessed')
  expect(upper.get('.sheet-backdrop').classes()).toContain('is-stacked')

  await upper.setProps({ open: false })
  await nextTick()
  expect(visibleSheetCount.value).toBe(1)
  expect(lower.get('.sheet').classes()).not.toContain('is-recessed')

  await lower.setProps({ open: false })
  await nextTick()
  expect(visibleSheetCount.value).toBe(0)
  expect(hasOpenSheet.value).toBe(false)

  upper.unmount()
  lower.unmount()
})

test('does not dismiss when user pauses 270ms after a fast drag before releasing', async () => {
  let now = 1000
  const perfSpy = vi.spyOn(performance, 'now').mockImplementation(() => now)
  const view = mount(BottomSheet, {
    props: { open: true, title: '编辑' },
    global: { stubs: { teleport: true } }
  })
  const grabber = view.get('.sheet-grabber')
  Object.defineProperty(grabber.element, 'setPointerCapture', { value: vi.fn() })

  try {
    grabber.element.dispatchEvent(new PointerEvent('pointerdown', { clientY: 100, pointerId: 1, bubbles: true }))
    now += 30
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 140, pointerId: 1, bubbles: true }))
    // Pause 270ms before releasing
    now += 270
    grabber.element.dispatchEvent(new PointerEvent('pointerup', { clientY: 140, pointerId: 1, bubbles: true }))
    await nextTick()
    expect(view.emitted('close')).toBeUndefined()

    // Pull back before releasing (100 -> 160 -> 148, release at 148) must NOT close
    grabber.element.dispatchEvent(new PointerEvent('pointerdown', { clientY: 100, pointerId: 2, bubbles: true }))
    now += 20
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 160, pointerId: 2, bubbles: true }))
    now += 15
    grabber.element.dispatchEvent(new PointerEvent('pointermove', { clientY: 148, pointerId: 2, bubbles: true }))
    now += 5
    grabber.element.dispatchEvent(new PointerEvent('pointerup', { clientY: 148, pointerId: 2, bubbles: true }))
    await nextTick()
    expect(view.emitted('close')).toBeUndefined()
  } finally {
    view.unmount()
    perfSpy.mockRestore()
  }
})

test('assigns higher z-index by open order and restores focus to lower sheet even if trigger became disabled', async () => {
  const firstInDom = mount(BottomSheet, { props: { open: false, title: '较先挂载的确认弹窗' } })
  const secondInDom = mount(BottomSheet, {
    props: { open: true, title: '较晚挂载的底层表单' },
    slots: { default: '<button type="button" class="inner-trigger">删除</button>' }
  })
  try {
    await nextTick()
    const innerTrigger = document.querySelector<HTMLButtonElement>('.inner-trigger')!
    innerTrigger.focus()
    expect(document.activeElement).toBe(innerTrigger)

    await firstInDom.setProps({ open: true })
    await nextTick()

    const backdrops = [...document.querySelectorAll<HTMLElement>('.sheet-backdrop')]
    expect(backdrops).toHaveLength(2)
    const firstZ = Number(backdrops[0]!.style.zIndex)
    const secondZ = Number(backdrops[1]!.style.zIndex)
    expect(firstZ).toBeGreaterThan(secondZ)

    // Disable the trigger inside lower sheet before closing upper sheet
    innerTrigger.disabled = true
    await firstInDom.setProps({ open: false })
    await nextTick()
    await nextTick()
    const remainingSheet = document.querySelector<HTMLElement>('.sheet')
    expect(remainingSheet?.hasAttribute('inert')).toBe(false)
    expect(document.activeElement).toBe(remainingSheet)
  } finally {
    firstInDom.unmount()
    secondInDom.unmount()
    document.body.removeAttribute('style')
  }
})
