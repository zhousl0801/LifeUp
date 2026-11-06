import { onBeforeUnmount, ref, watch, type Ref } from 'vue'

/** 金额变化时从当前显示值滚动到新值；减少动效偏好下直接显示结果。 */
export function useCountUp(
  source: Ref<number | null | undefined>,
  duration = 620
): Ref<number | null> {
  const display = ref<number | null>(source.value === null || source.value === undefined ? null : 0)
  let frame = 0
  let from = source.value ?? 0
  let startedAt = 0
  const reduced = import.meta.env.MODE === 'test'
    || (typeof window !== 'undefined'
      && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true)

  watch(source, (value) => {
    if (value === null || value === undefined) {
      cancelAnimationFrame(frame)
      display.value = null
      return
    }

    if (reduced) {
      cancelAnimationFrame(frame)
      display.value = value
      return
    }

    from = display.value ?? 0
    startedAt = performance.now()
    cancelAnimationFrame(frame)

    const step = (now: number) => {
      const progress = Math.min(1, (now - startedAt) / duration)
      const eased = 1 - Math.pow(1 - progress, 3)
      display.value = from + (value - from) * eased

      if (progress < 1) {
        frame = requestAnimationFrame(step)
      } else {
        display.value = value
      }
    }

    frame = requestAnimationFrame(step)
  }, { immediate: true })

  onBeforeUnmount(() => cancelAnimationFrame(frame))

  return display
}
