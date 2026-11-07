import type { Router, RouteLocationRaw } from 'vue-router'

export function navigateBack(router: Router, fallback: RouteLocationRaw = { name: 'home' }): void {
  if (router.options?.history.state.back) {
    router.back()
  } else {
    void router.push(fallback)
  }
}
