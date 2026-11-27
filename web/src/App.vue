<script setup lang="ts">
import { onBeforeUnmount, shallowRef, useTemplateRef, watch } from 'vue'
import AppMessage from './components/AppMessage.vue'
import ServiceStartup from './components/ServiceStartup.vue'
import UpdateBanner from './components/UpdateBanner.vue'
import { isReducedMotion } from './composables/useReducedMotion'
import { clearRouteMotion, useRouteMotion } from './utils/routeMotion'
import { syncStatusBarColor } from './utils/theme'

const serviceReady = shallowRef(false)
const routeStageRef = useTemplateRef<HTMLElement>('routeStage')
const { routeMotion, routeMotionEpoch, currentSurface, hasOpenSheet } = useRouteMotion()

let routeMotionTimer: number | undefined
let activeEnterTargets: HTMLElement[] = []

function clearEnterTargets(): void {
  window.clearTimeout(routeMotionTimer)
  routeMotionTimer = undefined
  for (const el of activeEnterTargets) {
    el.removeAttribute('data-route-enter')
  }
  activeEnterTargets = []
}

// 只为当次导航初次挂入的直系内容节点打一次性 data 属性标记（不被 Vue :class 更新覆盖），避免晚到的异步数据块重播入场动画。
watch(
  [serviceReady, routeMotionEpoch],
  ([ready]) => {
    clearEnterTargets()
    const kind = routeMotion.value
    if (!ready || kind === 'none') {
      return
    }

    const stage = routeStageRef.value
    if (!stage) {
      return
    }

    const selector = kind === 'modal-in'
      ? '.entry-screen > *'
      : '.page > :not(.app-nav):not(.asset-page-header)'
    const targets = [...stage.querySelectorAll<HTMLElement>(selector)]
    if (targets.length === 0) {
      clearRouteMotion()
      return
    }

    for (const el of targets) {
      el.setAttribute('data-route-enter', '1')
    }
    activeEnterTargets = targets

    const duration = isReducedMotion()
      ? 90
      : kind === 'tab' || kind === 'modal-out'
        ? 190
        : 250
    routeMotionTimer = window.setTimeout(() => {
      clearEnterTargets()
      clearRouteMotion()
    }, duration)
  },
  { flush: 'post' }
)

onBeforeUnmount(() => {
  clearEnterTargets()
})

// 服务就绪后或顶部色条表面/浮层状态变化后同步状态栏颜色，通过无过渡探针立即获取目标终色。
watch([serviceReady, currentSurface, hasOpenSheet], syncStatusBarColor, { flush: 'post' })
</script>

<template>
  <div
    class="status-bar-scrim"
    :data-surface="currentSurface"
    :data-sheet-open="hasOpenSheet ? 'true' : 'false'"
    aria-hidden="true"
  >
    <span class="status-bar-scrim-probe" aria-hidden="true" />
  </div>
  <UpdateBanner />
  <AppMessage />
  <template v-if="serviceReady">
    <div ref="routeStage" class="route-stage" :data-route-motion="routeMotion">
      <RouterView />
    </div>
  </template>
  <ServiceStartup v-else @ready="serviceReady = true" />
</template>
