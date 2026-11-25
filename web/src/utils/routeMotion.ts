import { computed, readonly, shallowRef } from 'vue'
import type { RouteLocationNormalizedLoaded } from 'vue-router'

export type RouteMotionKind = 'none' | 'tab' | 'push' | 'pop' | 'modal-in' | 'modal-out'
export type SurfaceMode = 'default' | 'home' | 'entry'

const tabRoutes = new Set(['ledger', 'home', 'capital', 'plans', 'mine'])

const parentMap: Record<string, Set<string>> = {
  assets: new Set(['home', 'capital']),
  cash: new Set(['home', 'assets']),
  funds: new Set(['home', 'assets', 'cash']),
  stocks: new Set(['home', 'assets', 'cash']),
  gold: new Set(['home', 'assets', 'cash']),
  provident: new Set(['home', 'assets', 'cash']),
  categories: new Set(['ledger', 'entry']),
  backup: new Set(['mine']),
  salary: new Set(['mine', 'capital'])
}

const routeMotion = shallowRef<RouteMotionKind>('none')
const routeMotionEpoch = shallowRef(0)
const currentSurface = shallowRef<SurfaceMode>('default')
const visibleSheetCount = shallowRef(0)

export function resolveSurfaceMode(routeName: string | symbol | null | undefined): SurfaceMode {
  if (routeName === 'home') return 'home'
  if (routeName === 'entry') return 'entry'
  return 'default'
}

export function classifyRouteMotion(
  to: Pick<RouteLocationNormalizedLoaded, 'name'>,
  from: Pick<RouteLocationNormalizedLoaded, 'name'>,
  isBackNavigation = false
): RouteMotionKind {
  const toName = typeof to.name === 'string' ? to.name : ''
  const fromName = typeof from.name === 'string' ? from.name : ''

  if (!fromName || !toName || toName === fromName) {
    return 'none'
  }

  if (toName === 'entry') {
    return 'modal-in'
  }

  if (fromName === 'entry') {
    return 'modal-out'
  }

  if (tabRoutes.has(toName) && tabRoutes.has(fromName)) {
    return 'tab'
  }

  if (parentMap[toName]?.has(fromName)) {
    return 'push'
  }

  if (parentMap[fromName]?.has(toName)) {
    return 'pop'
  }

  if (isBackNavigation && (tabRoutes.has(toName) || Boolean(parentMap[fromName]))) {
    return 'pop'
  }

  return 'none'
}

export function clearRouteMotion(): void {
  routeMotion.value = 'none'
}

export function commitRouteMotion(
  to: Pick<RouteLocationNormalizedLoaded, 'name'>,
  from: Pick<RouteLocationNormalizedLoaded, 'name'>,
  isBackNavigation = false
): void {
  routeMotion.value = classifyRouteMotion(to, from, isBackNavigation)
  routeMotionEpoch.value += 1
  currentSurface.value = resolveSurfaceMode(to.name)
}

export function setVisibleSheetCount(count: number): void {
  visibleSheetCount.value = Math.max(0, count)
}

export function useRouteMotion() {
  return {
    routeMotion: readonly(routeMotion),
    routeMotionEpoch: readonly(routeMotionEpoch),
    currentSurface: readonly(currentSurface),
    visibleSheetCount: readonly(visibleSheetCount),
    hasOpenSheet: computed(() => visibleSheetCount.value > 0)
  }
}
