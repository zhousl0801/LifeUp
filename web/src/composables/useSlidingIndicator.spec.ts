import { mount } from '@vue/test-utils'
import { defineComponent, nextTick, ref, useTemplateRef } from 'vue'
import { afterEach, describe, expect, test, vi } from 'vitest'
import { useSlidingIndicator } from './useSlidingIndicator'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('useSlidingIndicator', () => {
  test('subtracts border clientLeft and cancels pending cross-route rAF when option changes early', async () => {
    const queuedFrames = new Map<number, FrameRequestCallback>()
    let nextId = 1
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
      const id = nextId++
      queuedFrames.set(id, cb)
      return id
    })
    vi.stubGlobal('cancelAnimationFrame', (id: number) => {
      queuedFrames.delete(id)
    })

    const getRectSpy = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.classList.contains('track')) {
        return { left: 10, top: 0, width: 202, height: 40, right: 212, bottom: 40, x: 10, y: 0, toJSON: () => ({}) } as DOMRect
      }
      if (this.dataset.id === 'a') {
        return { left: 15, top: 4, width: 96, height: 32, right: 111, bottom: 36, x: 15, y: 4, toJSON: () => ({}) } as DOMRect
      }
      if (this.dataset.id === 'b') {
        return { left: 111, top: 4, width: 96, height: 32, right: 207, bottom: 36, x: 111, y: 4, toJSON: () => ({}) } as DOMRect
      }
      return { left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect
    })
    const clientLeftDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'clientLeft')
    Object.defineProperty(HTMLElement.prototype, 'clientLeft', {
      configurable: true,
      get() {
        return (this as HTMLElement).classList?.contains('track') ? 1 : 0
      }
    })

    const makeHarness = (initial: 'a' | 'b') =>
      defineComponent({
        setup() {
          const active = ref<'a' | 'b'>(initial)
          const track = useTemplateRef<HTMLElement>('track')
          const { ready, animated, indicatorStyle } = useSlidingIndicator(track, active, {
            memoryKey: 'spec-sliding-cancel'
          })
          return { active, ready, animated, indicatorStyle }
        },
        template: `
          <div ref="track" class="track">
            <span v-if="ready" class="pill" :style="indicatorStyle" />
            <button data-id="a" :class="{ active: active === 'a' }">A</button>
            <button data-id="b" :class="{ active: active === 'b' }">B</button>
          </div>
        `
      })

    try {
      // Mount 1 on 'a': x = 15 - 10 - 1 (clientLeft) = 4px
      const first = mount(makeHarness('a'))
      await nextTick()
      expect(first.get('.pill').attributes('style')).toContain('translate3d(4px, 0, 0)')
      first.unmount()

      // Mount 2 starts on 'b' (x = 100px), queues cross-route rAF from 4px -> 100px
      const second = mount(makeHarness('b'))
      await nextTick()
      expect(queuedFrames.size).toBe(1)

      // Switch back to 'a' BEFORE the cross-route rAF fires
      ;(second.vm as unknown as { active: 'a' | 'b' }).active = 'a'
      await nextTick()
      await nextTick()
      expect(queuedFrames.size).toBe(0)

      // Flushing any remaining frames must keep pill on 'a' (4px), not 'b' (100px)
      for (const cb of [...queuedFrames.values()]) cb(16)
      await nextTick()
      expect(second.get('.pill').attributes('style')).toContain('translate3d(4px, 0, 0)')
      second.unmount()
    } finally {
      getRectSpy.mockRestore()
      if (clientLeftDescriptor) {
        Object.defineProperty(HTMLElement.prototype, 'clientLeft', clientLeftDescriptor)
      } else {
        delete (HTMLElement.prototype as { clientLeft?: number }).clientLeft
      }
    }
  })
})
