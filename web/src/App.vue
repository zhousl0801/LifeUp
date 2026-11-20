<script setup lang="ts">
import { shallowRef } from 'vue'
import AppMessage from './components/AppMessage.vue'
import LoadingBar from './components/LoadingBar.vue'
import PendingMutationNotice from './components/PendingMutationNotice.vue'
import ServiceStartup from './components/ServiceStartup.vue'
import UpdateBanner from './components/UpdateBanner.vue'

const serviceReady = shallowRef(false)
</script>

<template>
  <div class="status-bar-scrim" aria-hidden="true"></div>
  <UpdateBanner />
  <AppMessage />
  <template v-if="serviceReady">
    <LoadingBar />
    <PendingMutationNotice />
    <RouterView />
  </template>
  <ServiceStartup v-else @ready="serviceReady = true" />
</template>
