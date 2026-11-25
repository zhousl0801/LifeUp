import { computed, nextTick, onBeforeUnmount, shallowRef, watch, type Ref, type WatchSource } from 'vue'
import { isReducedMotion } from './useReducedMotion'

interface IndicatorRect {
  x: number
  width: number
}

const memoryRects = new Map<string, IndicatorRect>()

export interface UseSlidingIndicatorOptions {
  activeSelector?: string
  memoryKey?: string
  watchExtra?: WatchSource | WatchSource[]
}

export function useSlidingIndicator(
  trackRef: Readonly<Ref<HTMLElement | null>>,
  activeSource: WatchSource,
  options: UseSlidingIndicatorOptions = {}
) {
  const { activeSelector = '.active', memoryKey } = options
  const ready = shallowRef(false)
  const animated = shallowRef(false)
  const baseWidth = shallowRef(0)
  const currentX = shallowRef(0)
  const currentScale = shallowRef(1)

  let resizeObserver: ResizeObserver | null = null
  let observedElements = new Set<Element>()
  let frameId = 0
  let lastRect: IndicatorRect | null = null
  let hasInitialSync = false

  function rectsEqual(a: IndicatorRect | null, b: IndicatorRect | null): boolean {
    if (!a || !b) return false
    return Math.abs(a.x - b.x) <= 0.5 && Math.abs(a.width - b.width) <= 0.5
  }

  function measureActive(): IndicatorRect | null {
    const track = trackRef.value
    if (!track) return null
    const activeEl = track.querySelector<HTMLElement>(activeSelector)
    if (!activeEl) return null

    const trackRect = track.getBoundingClientRect()
    const itemRect = activeEl.getBoundingClientRect()
    if (itemRect.width <= 0 || trackRect.width <= 0) {
      return null
    }

    return {
      x: itemRect.left - trackRect.left - track.clientLeft + track.scrollLeft,
      width: itemRect.width
    }
  }

  function applyRect(rect: IndicatorRect, shouldAnimate: boolean): void {
    if (baseWidth.value <= 0 || !shouldAnimate) {
      baseWidth.value = rect.width
      currentScale.value = 1
    } else {
      currentScale.value = rect.width / baseWidth.value
    }
    currentX.value = rect.x
    lastRect = rect
    ready.value = true
    animated.value = shouldAnimate && !isReducedMotion()

    if (memoryKey) {
      memoryRects.set(memoryKey, rect)
    }
  }

  function sync(allowAnimate: boolean): void {
    cancelAnimationFrame(frameId)
    frameId = 0
    const rect = measureActive()
    if (!rect) {
      lastRect = null
      ready.value = false
      return
    }
    applyRect(rect, allowAnimate && ready.value)
  }

  function syncFromResize(): void {
    const rect = measureActive()
    if (!rect) {
      cancelAnimationFrame(frameId)
      frameId = 0
      lastRect = null
      ready.value = false
      return
    }
    // 尺寸未变时保留当前正在进行的跨路由或切换过渡，避免观察回调打断动画。
    if (rectsEqual(lastRect, rect)) {
      return
    }
    cancelAnimationFrame(frameId)
    frameId = 0
    applyRect(rect, false)
  }

  function observeTrackAndItems(track: HTMLElement | null): void {
    if (typeof ResizeObserver === 'undefined') {
      return
    }
    if (!track) {
      resizeObserver?.disconnect()
      resizeObserver = null
      observedElements.clear()
      return
    }
    if (!resizeObserver) {
      resizeObserver = new ResizeObserver(() => {
        syncFromResize()
      })
    }

    const targets = new Set<Element>([track])
    track.querySelectorAll('button, a').forEach((child) => targets.add(child))

    for (const el of observedElements) {
      if (!targets.has(el)) {
        resizeObserver.unobserve(el)
      }
    }
    for (const el of targets) {
      if (!observedElements.has(el)) {
        resizeObserver.observe(el)
      }
    }
    observedElements = targets
  }

  function initOrSyncOnTrackMount(track: HTMLElement | null): void {
    cancelAnimationFrame(frameId)
    observeTrackAndItems(track)

    if (!track) {
      lastRect = null
      ready.value = false
      animated.value = false
      return
    }

    const rect = measureActive()
    if (!rect) {
      lastRect = null
      ready.value = false
      return
    }

    const previous = !hasInitialSync && memoryKey ? memoryRects.get(memoryKey) : undefined
    hasInitialSync = true

    if (
      previous
      && !isReducedMotion()
      && (Math.abs(previous.x - rect.x) > 1 || Math.abs(previous.width - rect.width) > 1)
    ) {
      baseWidth.value = rect.width
      currentX.value = previous.x
      currentScale.value = previous.width / rect.width
      lastRect = rect
      ready.value = true
      animated.value = false
      frameId = requestAnimationFrame(() => {
        applyRect(rect, true)
      })
    } else {
      applyRect(rect, false)
    }
  }

  watch(
    trackRef,
    (track) => {
      initOrSyncOnTrackMount(track)
    },
    { immediate: true, flush: 'post' }
  )

  watch(activeSource, async () => {
    await nextTick()
    observeTrackAndItems(trackRef.value)
    sync(true)
  })

  if (options.watchExtra) {
    watch(options.watchExtra, async () => {
      await nextTick()
      observeTrackAndItems(trackRef.value)
      sync(false)
    })
  }

  onBeforeUnmount(() => {
    cancelAnimationFrame(frameId)
    resizeObserver?.disconnect()
    resizeObserver = null
    observedElements.clear()
  })

  const indicatorStyle = computed(() => {
    if (!ready.value || baseWidth.value <= 0) {
      return undefined
    }
    const transform = Math.abs(currentScale.value - 1) < 0.002
      ? `translate3d(${currentX.value}px, 0, 0)`
      : `translate3d(${currentX.value}px, 0, 0) scaleX(${currentScale.value})`

    return {
      width: `${baseWidth.value}px`,
      transform
    }
  })

  return {
    ready,
    animated,
    indicatorStyle,
    sync
  }
}
