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
  document.body.style.overflow = 'auto'
  const trigger = document.createElement('button')
  document.body.append(trigger)
  trigger.focus()
  const lower = mount(BottomSheet, { props: { open: true, title: '编辑' } })
  await nextTick()
  expect(document.activeElement?.getAttribute('role')).toBe('dialog')
  const upper = mount(BottomSheet, { props: { open: true, title: '放弃修改' } })
  await nextTick()

  await lower.setProps({ open: false })
  expect(document.body.style.overflow).toBe('hidden')
  await upper.setProps({ open: false })
  expect(document.body.style.overflow).toBe('auto')
  expect(document.activeElement).toBe(trigger)
  trigger.remove()
  upper.unmount()
  lower.unmount()
  document.body.style.removeProperty('overflow')
})
