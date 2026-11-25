import { readonly, shallowRef, type Ref } from 'vue'

const reducedMotionQuery = '(prefers-reduced-motion: reduce)'
const reduced = shallowRef(readReducedMotion())

let mediaQuery: MediaQueryList | null = null
let listenerRegistered = false

function readReducedMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return false
  }

  return Boolean(window.matchMedia(reducedMotionQuery)?.matches)
}

function ensureListener(): void {
  if (listenerRegistered || typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return
  }

  listenerRegistered = true
  mediaQuery = window.matchMedia(reducedMotionQuery)
  reduced.value = Boolean(mediaQuery?.matches)
  mediaQuery?.addEventListener?.('change', (event) => {
    reduced.value = event.matches
  })
}

export function isReducedMotion(): boolean {
  const current = readReducedMotion()
  if (reduced.value !== current) {
    reduced.value = current
  }
  return current
}

export function useReducedMotion(): Readonly<Ref<boolean>> {
  ensureListener()
  reduced.value = readReducedMotion()
  return readonly(reduced)
}
