<script lang="ts">
import { shallowRef } from 'vue'
import { setVisibleSheetCount } from '../utils/routeMotion'

// 多个 sheet 叠加时，只让最上层处理 Tab / Escape，避免下层同时抢焦点。
type OpenSheet = {
  element: () => HTMLElement | null
  previousFocus: HTMLElement | null
}

const openSheets = shallowRef<OpenSheet[]>([])
let releasePageScroll: (() => void) | undefined

function syncSheetCount(): void {
  setVisibleSheetCount(openSheets.value.length)
}

function lockPageScroll(): () => void {
  const { style } = document.body
  const { scrollX, scrollY } = window
  const { overflow, position, top, width } = style

  // iOS 主屏幕应用仍可能滚动设置了 overflow: hidden 的 body。
  Object.assign(style, {
    overflow: 'hidden',
    position: 'fixed',
    top: `${-scrollY}px`,
    width: '100%'
  })

  return () => {
    Object.assign(style, { overflow, position, top, width })
    if (window.scrollX !== scrollX || window.scrollY !== scrollY) {
      window.scrollTo({ left: scrollX, top: scrollY, behavior: 'instant' })
    }
  }
}
</script>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, shallowRef as localShallowRef, useId, watch } from 'vue'
import { isReducedMotion } from '../composables/useReducedMotion'

const props = defineProps<{
  open: boolean
  title: string
  subtitle?: string
  closeDisabled?: boolean
  closeIcon?: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const sheetId = useId()
const sheet = localShallowRef<HTMLElement | null>(null)
const dragY = localShallowRef(0)
const dragging = localShallowRef(false)
const keyboardInset = localShallowRef(0)
const viewportHeight = localShallowRef<number | null>(null)
const registration: OpenSheet = { element: () => sheet.value, previousFocus: null }

interface DragSample {
  y: number
  time: number
}

let dragStartY: number | null = null
let dragPointerId: number | null = null
let dragCaptureTarget: HTMLElement | null = null
let dragCaptured = false
let dragBaseOffset = 0
let dragSheetHeight = 360
let dragSamples: DragSample[] = []
let wasOpen = false
let viewportBound = false
let keyboardResizing = false
let keyboardResizeTimer: number | undefined
const lastStackIndex = shallowRef(0)

const stackIndex = computed(() => openSheets.value.indexOf(registration))
const backdropZIndex = computed(() => 50 + (stackIndex.value >= 0 ? stackIndex.value : lastStackIndex.value))
const isRecessed = computed(() => stackIndex.value >= 0 && stackIndex.value < openSheets.value.length - 1)
const isStackedOverlay = computed(() => (stackIndex.value >= 0 ? stackIndex.value : lastStackIndex.value) > 0)

const backdropOpacity = computed(() => {
  if (!dragging.value || dragY.value <= 0) {
    return undefined
  }
  const ratio = Math.min(1, Math.max(0, dragY.value / dragSheetHeight))
  return String(Math.max(0.18, 1 - ratio * 0.82))
})

function isDesktopDialog(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(min-width: 600px)').matches
}

function readVisualTranslateY(): number {
  const el = sheet.value
  if (!el || typeof window === 'undefined' || typeof window.getComputedStyle !== 'function') {
    return 0
  }
  const transform = window.getComputedStyle(el).transform
  if (!transform || transform === 'none') {
    return 0
  }
  const matrixMatch = transform.match(/^matrix\(([^)]+)\)$/)
  if (matrixMatch) {
    const parts = matrixMatch[1]!.split(',').map(Number)
    return Number.isFinite(parts[5]) ? parts[5]! : 0
  }
  const matrix3dMatch = transform.match(/^matrix3d\(([^)]+)\)$/)
  if (matrix3dMatch) {
    const parts = matrix3dMatch[1]!.split(',').map(Number)
    return Number.isFinite(parts[13]) ? parts[13]! : 0
  }
  return 0
}

function scrollFocusedControlIntoView(): void {
  const active = document.activeElement
  if (!(active instanceof HTMLElement) || !sheet.value?.contains(active)) return
  if (!active.matches('input, textarea, select')) return
  const viewport = window.visualViewport
  if (viewport) {
    const rect = active.getBoundingClientRect()
    const visibleTop = viewport.offsetTop + 8
    const visibleBottom = viewport.offsetTop + viewport.height - 8
    if (rect.top >= visibleTop && rect.bottom <= visibleBottom) {
      return
    }
  }
  active.scrollIntoView?.({ block: 'nearest' })
}

function syncVisualViewport(): void {
  const viewport = window.visualViewport
  if (!viewport) {
    keyboardInset.value = 0
    viewportHeight.value = null
    return
  }

  const layoutHeight = window.innerHeight || document.documentElement.clientHeight
  const occluded = Math.max(0, Math.round(layoutHeight - viewport.height - viewport.offsetTop))
  const nextInset = occluded > 80 ? occluded : 0
  if (nextInset !== keyboardInset.value) {
    keyboardResizing = true
    if (dragging.value) {
      resetDrag()
    }
    window.clearTimeout(keyboardResizeTimer)
    keyboardResizeTimer = window.setTimeout(() => {
      keyboardResizing = false
    }, 180)
  }

  if (nextInset > 0) {
    keyboardInset.value = nextInset
    viewportHeight.value = Math.max(200, Math.floor(viewport.height))
    void nextTick(scrollFocusedControlIntoView)
  } else {
    keyboardInset.value = 0
    viewportHeight.value = null
  }
}

function bindVisualViewport(): void {
  if (viewportBound || !window.visualViewport) return
  viewportBound = true
  window.visualViewport.addEventListener('resize', syncVisualViewport)
  window.visualViewport.addEventListener('scroll', syncVisualViewport)
  syncVisualViewport()
}

function unbindVisualViewport(): void {
  if (viewportBound && window.visualViewport) {
    window.visualViewport.removeEventListener('resize', syncVisualViewport)
    window.visualViewport.removeEventListener('scroll', syncVisualViewport)
  }
  viewportBound = false
  keyboardResizing = false
  window.clearTimeout(keyboardResizeTimer)
  keyboardInset.value = 0
  viewportHeight.value = null
}

function handleFocusIn(event: FocusEvent): void {
  const target = event.target
  if (!(target instanceof HTMLElement) || !target.matches('input, textarea, select')) return
  syncVisualViewport()
  void nextTick(scrollFocusedControlIntoView)
}

watch(
  () => props.closeDisabled,
  (disabled) => {
    if (disabled && dragPointerId !== null) {
      resetDrag()
    }
  }
)

watch(
  () => props.open,
  async (open) => {
    if (open) {
      wasOpen = true

      if (openSheets.value.length === 0) {
        releasePageScroll = lockPageScroll()
      }
      openSheets.value = [...openSheets.value, registration]
      lastStackIndex.value = openSheets.value.length - 1
      syncSheetCount()
      registration.previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
      resetDrag()
      bindVisualViewport()
      await nextTick()
      // 不自动聚焦输入框，避免打开 sheet 就弹出键盘；先把焦点放在对话框本身。
      if (openSheets.value.at(-1) === registration) sheet.value?.focus({ preventScroll: true })
    } else if (wasOpen) {
      wasOpen = false
      unbindVisualViewport()
      unregisterSheet()
    }
  },
  { immediate: true }
)

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeydown)
  unbindVisualViewport()
  resetDrag()
  unregisterSheet()
})

function unregisterSheet(): void {
  const index = openSheets.value.indexOf(registration)
  if (index < 0) return

  const wasTop = index === openSheets.value.length - 1
  const previousFocus = registration.previousFocus
  const closingElement = sheet.value
  const nextStack = [...openSheets.value]
  nextStack.splice(index, 1)
  openSheets.value = nextStack
  syncSheetCount()
  registration.previousFocus = null

  for (const upper of nextStack.slice(index)) {
    if (closingElement?.contains(upper.previousFocus)) upper.previousFocus = previousFocus
  }

  if (nextStack.length === 0) {
    releasePageScroll?.()
    releasePageScroll = undefined
  }
  if (!wasTop) return

  const nextRegistration = nextStack.at(-1)
  if (!nextRegistration) {
    if (previousFocus?.isConnected) {
      previousFocus.focus({ preventScroll: true })
    }
    if (closingElement && document.activeElement instanceof HTMLElement && closingElement.contains(document.activeElement)) {
      document.activeElement.blur()
    }
    return
  }

  // 下层浮层在下一轮 DOM 刷新后才会移除 inert，待刷新后再恢复焦点并复核当前顶层；若原触发元素不可聚焦则回退到下层对话框。
  void nextTick(() => {
    if (openSheets.value.at(-1) !== nextRegistration) {
      return
    }
    const nextSheet = nextRegistration.element()
    if (previousFocus?.isConnected && nextSheet?.contains(previousFocus)) {
      previousFocus.focus({ preventScroll: true })
      if (document.activeElement === previousFocus) {
        return
      }
    }
    nextSheet?.focus({ preventScroll: true })
  })
}

function close(): void {
  if (!props.closeDisabled) {
    emit('close')
  }
}

function focusables(): HTMLElement[] {
  if (!sheet.value) {
    return []
  }

  return [...sheet.value.querySelectorAll<HTMLElement>(
    'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
  )].filter((element) =>
    element.getAttribute('aria-hidden') !== 'true'
    && (element.offsetWidth > 0 || element.offsetHeight > 0 || element.getClientRects().length > 0)
  )
}

function handleKeydown(event: KeyboardEvent): void {
  if (!props.open || openSheets.value.at(-1) !== registration) {
    return
  }

  if (event.key === 'Escape') {
    event.preventDefault()
    close()
    return
  }

  if (event.key !== 'Tab') {
    return
  }

  const nodes = focusables()

  if (nodes.length === 0) {
    event.preventDefault()
    sheet.value?.focus()
    return
  }

  const first = nodes[0]!
  const last = nodes[nodes.length - 1]!

  if (event.shiftKey && (document.activeElement === first || document.activeElement === sheet.value)) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

function isInteractiveTarget(target: EventTarget | null): boolean {
  return target instanceof Element
    && Boolean(target.closest('button, a, input, select, textarea, label, [role="button"]'))
}

function startDrag(event: PointerEvent, fromHeader = false): void {
  if (props.closeDisabled || dragPointerId !== null || event.button !== 0 || isDesktopDialog() || keyboardResizing) {
    return
  }

  if (fromHeader && isInteractiveTarget(event.target)) {
    return
  }

  const target = event.currentTarget as HTMLElement
  dragPointerId = event.pointerId
  dragStartY = event.clientY
  dragCaptureTarget = target
  dragSheetHeight = Math.max(240, sheet.value?.offsetHeight || 360)
  dragBaseOffset = Math.max(0, readVisualTranslateY())
  dragSamples = [{ y: event.clientY, time: performance.now() }]

  if (!fromHeader) {
    dragging.value = true
    dragCaptured = true
    dragY.value = dragBaseOffset
    target.setPointerCapture?.(event.pointerId)
  } else {
    dragCaptured = false
  }
}

function moveDrag(event: PointerEvent): void {
  if (event.pointerId !== dragPointerId || dragStartY === null || props.closeDisabled || keyboardResizing) {
    return
  }

  const rawDelta = event.clientY - dragStartY

  if (!dragCaptured) {
    if (Math.abs(rawDelta) < 6) {
      return
    }
    dragCaptured = true
    dragging.value = true
    dragCaptureTarget?.setPointerCapture?.(event.pointerId)
  }

  const now = performance.now()
  dragSamples.push({ y: event.clientY, time: now })
  const cutoff = now - 80
  while (dragSamples.length > 2 && dragSamples[0]!.time < cutoff) {
    dragSamples.shift()
  }

  const totalDelta = dragBaseOffset + rawDelta
  if (totalDelta >= 0) {
    dragY.value = totalDelta
  } else if (isReducedMotion()) {
    dragY.value = 0
  } else {
    dragY.value = -24 * (1 - Math.exp(-Math.abs(totalDelta) / 48))
  }
}

function resetDrag(): void {
  if (dragCaptureTarget && dragPointerId !== null && dragCaptureTarget.hasPointerCapture?.(dragPointerId)) {
    dragCaptureTarget.releasePointerCapture(dragPointerId)
  }
  dragging.value = false
  dragCaptured = false
  dragCaptureTarget = null
  dragStartY = null
  dragPointerId = null
  dragBaseOffset = 0
  dragSamples = []
  dragY.value = 0
}

function cancelDrag(event: PointerEvent): void {
  if (event.pointerId === dragPointerId) resetDrag()
}

function shouldFlickClose(releaseY: number, releaseTime: number): boolean {
  if (dragY.value < 24 || dragSamples.length < 2) {
    return false
  }

  const samples = [...dragSamples]
  const lastRecorded = samples.at(-1)!
  if (releaseTime > lastRecorded.time || releaseY !== lastRecorded.y) {
    samples.push({ y: releaseY, time: releaseTime })
  }

  const cutoff = releaseTime - 80
  while (samples.length > 1 && samples[0]!.time < cutoff) {
    samples.shift()
  }

  if (samples.length < 2) {
    return false
  }

  const latest = samples.at(-1)!
  // 回溯找到最后一次真实位移，避免松手与末次 pointermove 同位时掩盖向上回拉。
  let lastMoved: DragSample | null = null
  for (let i = samples.length - 2; i >= 0; i--) {
    if (samples[i]!.y !== latest.y) {
      lastMoved = samples[i]!
      break
    }
  }
  if (!lastMoved || latest.y < lastMoved.y) {
    return false
  }

  const oldest = samples[0]!
  const dt = latest.time - oldest.time
  if (dt <= 0) {
    return false
  }

  return (latest.y - oldest.y) / dt >= 0.75
}

async function endDrag(event: PointerEvent): Promise<void> {
  if (event.pointerId !== dragPointerId) return

  const releaseTime = performance.now()
  const sheetHeight = sheet.value?.offsetHeight ?? 0
  const distanceThreshold = sheetHeight > 0 ? Math.min(96, Math.round(sheetHeight * 0.28)) : 96
  const shouldClose = dragging.value
    && !props.closeDisabled
    && (dragY.value > distanceThreshold || shouldFlickClose(event.clientY, releaseTime))

  if (!shouldClose) {
    resetDrag()
    return
  }

  if (dragCaptureTarget && dragCaptureTarget.hasPointerCapture?.(event.pointerId)) {
    dragCaptureTarget.releasePointerCapture(event.pointerId)
  }
  dragging.value = false
  dragCaptured = false
  dragCaptureTarget = null
  dragStartY = null
  dragPointerId = null
  dragBaseOffset = 0
  dragSamples = []
  close()
  await nextTick()

  // When closing opens a confirmation instead, return the underlying sheet to rest.
  if (props.open) resetDrag()
}
</script>

<template>
  <Teleport to="body">
    <Transition name="sheet">
      <div
        v-if="open"
        class="sheet-backdrop"
        :class="{ 'is-stacked': isStackedOverlay, 'is-dragging': dragging }"
        :style="{
          zIndex: backdropZIndex,
          ...(keyboardInset > 0 ? { paddingBottom: `${keyboardInset}px` } : {}),
          ...(backdropOpacity !== undefined ? { '--sheet-backdrop-opacity': backdropOpacity } : {})
        }"
        @click.self="close"
      >
        <section
          ref="sheet"
          class="sheet"
          :class="{ dragging, 'keyboard-open': keyboardInset > 0, 'is-recessed': isRecessed }"
          :style="{
            '--sheet-drag-y': `${dragY}px`,
            ...(viewportHeight !== null ? { '--sheet-max-height': `${Math.max(200, viewportHeight - 16)}px` } : {})
          }"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="sheetId"
          :inert="isRecessed || undefined"
          tabindex="-1"
          @focusin="handleFocusIn"
        >
          <div
            class="sheet-grabber"
            aria-hidden="true"
            @pointerdown="startDrag($event, false)"
            @pointermove="moveDrag"
            @pointerup="endDrag"
            @pointercancel="cancelDrag"
            @lostpointercapture="cancelDrag"
          >
            <span />
          </div>

          <header
            class="sheet-head"
            @pointerdown="startDrag($event, true)"
            @pointermove="moveDrag"
            @pointerup="endDrag"
            @pointercancel="cancelDrag"
            @lostpointercapture="cancelDrag"
          >
            <div>
              <h2 :id="sheetId">{{ title }}</h2>
              <p v-if="subtitle">{{ subtitle }}</p>
            </div>
            <button type="button" class="sheet-close" :disabled="closeDisabled" aria-label="关闭" @click="close">
              <svg v-if="closeIcon" class="sheet-close-icon" viewBox="0 0 24 24" fill="none"
                stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
              <template v-else>关闭</template>
            </button>
          </header>
          <div class="sheet-body">
            <slot />
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.sheet-backdrop {
  --sheet-backdrop-opacity: 1;
  box-sizing: border-box;
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  justify-content: center;
  align-items: flex-end;
  isolation: isolate;
}

.sheet-backdrop::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -1;
  background: var(--color-overlay);
  opacity: var(--sheet-backdrop-opacity);
  transition: opacity var(--motion-sheet-out) var(--ease-move);
  pointer-events: none;
}

.sheet-backdrop.is-stacked::before {
  background: color-mix(in srgb, var(--color-overlay) 46%, transparent);
}

.sheet-backdrop.is-dragging::before {
  transition: none;
}

.sheet {
  box-sizing: border-box;
  width: min(100%, 430px);
  max-height: var(--sheet-max-height, 88dvh);
  overflow-y: auto;
  overscroll-behavior-y: contain;
  padding: 0 var(--page-gutter) calc(20px + env(safe-area-inset-bottom));
  border-radius: var(--radius-sheet) var(--radius-sheet) 0 0;
  background: var(--color-bg-page);
  box-shadow: 0 -8px 30px var(--color-shadow);
  outline: none;
  transform: translate3d(0, var(--sheet-drag-y, 0px), 0);
  transition:
    transform var(--motion-sheet-in) var(--ease-enter),
    opacity var(--motion-feedback) var(--ease-move);
}

.sheet.is-recessed {
  transform: translate3d(0, -6px, 0) scale(0.97);
  opacity: 0.92;
}

.sheet.keyboard-open {
  padding-bottom: 16px;
}

.sheet.dragging {
  transition: none;
}

.sheet-enter-active {
  transition: opacity var(--motion-sheet-in) var(--ease-enter);
}

.sheet-leave-active {
  transition: opacity var(--motion-sheet-out) var(--ease-exit);
  pointer-events: none;
}

.sheet-enter-from,
.sheet-leave-to {
  opacity: 0;
}

.sheet-enter-active .sheet {
  transition: transform var(--motion-sheet-in) var(--ease-enter);
}

.sheet-leave-active .sheet {
  transition: transform var(--motion-sheet-out) var(--ease-exit);
}

.sheet-enter-from .sheet,
.sheet-leave-to .sheet {
  transform: translate3d(0, 100%, 0);
}

.sheet-backdrop.is-dragging:not(.sheet-leave-active) .sheet,
.sheet-backdrop:not(.sheet-leave-active) .sheet.dragging {
  transition: none;
}

.sheet-grabber {
  display: grid;
  place-items: center;
  height: 44px;
  margin: 0 calc(var(--page-gutter) * -1) -14px;
  padding-bottom: 14px;
  box-sizing: border-box;
  touch-action: none;
  cursor: grab;
}

.sheet-grabber:active {
  cursor: grabbing;
}

.sheet-grabber span {
  width: 36px;
  height: 5px;
  border-radius: var(--radius-pill);
  background: var(--color-border-default);
}

.sheet-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 12px;
  touch-action: pan-x;
}

.sheet-head h2 {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.sheet-head p {
  margin: 3px 0 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  line-height: 1.45;
}

.sheet-close {
  flex-shrink: 0;
  min-width: 32px;
  min-height: 32px;
  margin: -2px 0 0;
  padding: 0 10px;
  border: 0;
  border-radius: var(--radius-pill);
  background: color-mix(in srgb, var(--color-text-primary) 6%, transparent);
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-weight: 600;
  white-space: nowrap;
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-settle);
}

.sheet-close-icon {
  display: block;
  width: 16px;
  height: 16px;
  margin: auto;
}

@media (hover: hover) and (pointer: fine) {
  .sheet-close:hover:not(:disabled) {
    background: var(--color-brand-soft);
    color: var(--color-brand);
  }
}

.sheet-close:active:not(:disabled) {
  transform: scale(0.94);
  transition-duration: var(--motion-press-in);
}

.sheet-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

@media (min-width: 600px) {
  .sheet-backdrop {
    align-items: center;
    padding: 20px;
  }

  .sheet-grabber {
    display: none;
  }

  .sheet {
    padding-top: 20px;
    border-radius: var(--radius-sheet);
    box-shadow: var(--shadow-float);
  }

  .sheet-head {
    touch-action: auto;
  }

  .sheet-enter-active {
    transition: opacity var(--motion-dialog-in) var(--ease-enter);
  }

  .sheet-leave-active {
    transition: opacity var(--motion-dialog-out) var(--ease-exit);
  }

  .sheet-enter-active .sheet {
    transition:
      transform var(--motion-dialog-in) var(--ease-enter),
      opacity var(--motion-dialog-in) var(--ease-enter);
  }

  .sheet-leave-active .sheet {
    transition:
      transform var(--motion-dialog-out) var(--ease-exit),
      opacity var(--motion-dialog-out) var(--ease-exit);
  }

  .sheet-enter-from .sheet,
  .sheet-leave-to .sheet {
    opacity: 0;
    transform: translate3d(0, 6px, 0) scale(0.985);
  }
}

@media (prefers-reduced-motion: reduce) {
  .sheet-backdrop::before {
    transition: opacity var(--motion-reduced-fade) var(--ease-linear);
  }

  .sheet {
    transition: opacity var(--motion-reduced-fade) var(--ease-linear);
  }

  .sheet.is-recessed {
    transform: none;
  }

  .sheet-enter-active,
  .sheet-leave-active {
    transition: opacity var(--motion-reduced-fade) var(--ease-linear);
  }

  .sheet-enter-active .sheet,
  .sheet-leave-active .sheet {
    transition: opacity var(--motion-reduced-fade) var(--ease-linear);
  }

  .sheet-enter-from .sheet,
  .sheet-leave-to .sheet {
    opacity: 0;
    transform: none;
  }

  .sheet-close:active:not(:disabled) {
    transform: none;
  }
}
</style>
