<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import AppMessage from './components/AppMessage.vue'
import PendingMutationNotice from './components/PendingMutationNotice.vue'
import ServiceStartup from './components/ServiceStartup.vue'
import UpdateBanner from './components/UpdateBanner.vue'
import { syncStatusBarColor } from './utils/theme'

const serviceReady = shallowRef(false)

// 服务就绪后首个页面才渲染，渲染完成再同步一次状态栏颜色；后续路由切换由 router.afterEach 负责。
watch(serviceReady, syncStatusBarColor, { flush: 'post' })
</script>

<template>
  <div class="status-bar-scrim" aria-hidden="true"></div>
  <UpdateBanner />
  <AppMessage />
  <template v-if="serviceReady">
    <PendingMutationNotice />
    <RouterView />
  </template>
  <ServiceStartup v-else @ready="serviceReady = true" />
</template>
