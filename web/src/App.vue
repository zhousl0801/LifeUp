<script setup lang="ts">
import AppMessage from './components/AppMessage.vue'
import LoadingBar from './components/LoadingBar.vue'
import PendingMutationNotice from './components/PendingMutationNotice.vue'
import UpdateBanner from './components/UpdateBanner.vue'
</script>

<template>
  <LoadingBar />
  <PendingMutationNotice />
  <UpdateBanner />
  <AppMessage />
  <RouterView />
</template>
