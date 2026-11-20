<script lang="ts">
// 多个 sheet 叠加时，只让最上层处理 Tab / Escape，避免下层同时抢焦点。
type OpenSheet = {
  element: () => HTMLElement | null
  previousFocus: HTMLElement | null
}

const openSheets: OpenSheet[] = []
let releasePageScroll: (() => void) | undefined

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
import { nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

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
const sheet = ref<HTMLElement | null>(null)
const dragY = ref(0)
const dragging = ref(false)
const registration: OpenSheet = { element: () => sheet.value, previousFocus: null }
let dragStartY: number | null = null
let dragPointerId: number | null = null
let wasOpen = false

watch(
  () => props.open,
  async (open) => {
    if (open) {
      wasOpen = true

      if (openSheets.length === 0) {
        releasePageScroll = lockPageScroll()
      }
      openSheets.push(registration)
      registration.previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
      resetDrag()
      await nextTick()
      // 不自动聚焦输入框，避免打开 sheet 就弹出键盘；先把焦点放在对话框本身。
      if (openSheets.at(-1) === registration) sheet.value?.focus({ preventScroll: true })
    } else if (wasOpen) {
      wasOpen = false
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
  unregisterSheet()
})

function unregisterSheet(): void {
  const index = openSheets.indexOf(registration)
  if (index < 0) return

  const wasTop = index === openSheets.length - 1
  const previousFocus = registration.previousFocus
  const closingElement = sheet.value
  openSheets.splice(index, 1)
  registration.previousFocus = null

  for (const upper of openSheets.slice(index)) {
    if (closingElement?.contains(upper.previousFocus)) upper.previousFocus = previousFocus
  }

  if (openSheets.length === 0) {
    releasePageScroll?.()
    releasePageScroll = undefined
  }
  if (!wasTop) return

  const nextSheet = openSheets.at(-1)?.element()
  if (previousFocus?.isConnected && (!nextSheet || nextSheet.contains(previousFocus))) {
    previousFocus.focus({ preventScroll: true })
  } else {
    nextSheet?.focus({ preventScroll: true })
  }
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
  if (!props.open || openSheets.at(-1) !== registration) {
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

function startDrag(event: PointerEvent): void {
  if (props.closeDisabled || dragging.value || event.button !== 0) {
    return
  }

  dragPointerId = event.pointerId
  dragStartY = event.clientY
  dragging.value = true
  dragY.value = 0
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
}

function moveDrag(event: PointerEvent): void {
  if (event.pointerId !== dragPointerId || dragStartY === null) {
    return
  }

  dragY.value = Math.max(0, event.clientY - dragStartY)
}

function resetDrag(): void {
  dragging.value = false
  dragStartY = null
  dragPointerId = null
  dragY.value = 0
}

function cancelDrag(event: PointerEvent): void {
  if (event.pointerId === dragPointerId) resetDrag()
}

async function endDrag(event: PointerEvent): Promise<void> {
  if (event.pointerId !== dragPointerId) return

  if (dragY.value <= 96 || props.closeDisabled) {
    resetDrag()
    return
  }

  dragging.value = false
  dragStartY = null
  dragPointerId = null
  close()
  await nextTick()

  // When closing opens a confirmation instead, return the underlying sheet to rest.
  if (props.open) resetDrag()
}
</script>

<template>
  <Teleport to="body">
    <Transition name="sheet">
      <div v-if="open" class="sheet-backdrop" @click.self="close">
        <section
          ref="sheet"
          class="sheet"
          :class="{ dragging }"
          :style="{ '--sheet-drag-y': `${dragY}px` }"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="sheetId"
          tabindex="-1"
        >
          <div
            class="sheet-grabber"
            aria-hidden="true"
            @pointerdown="startDrag"
            @pointermove="moveDrag"
            @pointerup="endDrag"
            @pointercancel="cancelDrag"
            @lostpointercapture="cancelDrag"
          >
            <span />
          </div>

          <header class="sheet-head">
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
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  justify-content: center;
  align-items: flex-end;
  background: var(--color-overlay);
}

.sheet {
  box-sizing: border-box;
  width: min(100%, 430px);
  max-height: 88dvh;
  overflow-y: auto;
  overscroll-behavior-y: contain;
  padding: 0 var(--page-gutter) calc(20px + env(safe-area-inset-bottom));
  border-radius: var(--radius-sheet) var(--radius-sheet) 0 0;
  background: var(--color-bg-page);
  box-shadow: 0 -8px 30px var(--color-shadow);
  outline: none;
  transform: translateY(var(--sheet-drag-y, 0px));
  transition: transform var(--motion-sheet) var(--ease-sheet);
}

.sheet.dragging {
  transition: none;
}

.sheet-enter-active,
.sheet-leave-active {
  transition: opacity var(--motion-sheet) ease;
}

.sheet-enter-from,
.sheet-leave-to {
  opacity: 0;
}

.sheet-enter-active .sheet,
.sheet-leave-active .sheet {
  transition: transform var(--motion-sheet) var(--ease-sheet);
}

.sheet-enter-from .sheet,
.sheet-leave-to .sheet {
  transform: translateY(100%);
}

.sheet-grabber {
  display: grid;
  place-items: center;
  height: 28px;
  margin: 0 calc(var(--page-gutter) * -1);
  touch-action: none;
  cursor: grab;
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
}

.sheet-close-icon {
  display: block;
  width: 16px;
  height: 16px;
  margin: auto;
}

.sheet-close:hover:not(:disabled) {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}

.sheet-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

@media (min-width: 600px) {
  .sheet-backdrop {
    align-items: center;
  }

  .sheet {
    border-radius: var(--radius-sheet);
  }
}

@media (prefers-reduced-motion: reduce) {
  .sheet,
  .sheet-enter-active .sheet,
  .sheet-leave-active .sheet {
    transition: none;
  }
}
</style>
