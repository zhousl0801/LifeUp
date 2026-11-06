<script setup lang="ts">
import LoadingBar from './components/LoadingBar.vue'
import PendingMutationNotice from './components/PendingMutationNotice.vue'
import UpdateBanner from './components/UpdateBanner.vue'
</script>

<template>
  <LoadingBar />
  <PendingMutationNotice />
  <UpdateBanner />
  <RouterView v-slot="{ Component }">
    <Transition name="page" mode="out-in">
      <component :is="Component" />
    </Transition>
  </RouterView>
</template>
