<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { activeRequestCount } from '../api/client'

const visible = ref(false)
let showTimer: ReturnType<typeof setTimeout> | null = null
let hideTimer: ReturnType<typeof setTimeout> | null = null
let active = false

function clearTimers(): void {
  if (showTimer) {
    clearTimeout(showTimer)
    showTimer = null
  }

  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
}

watch(activeRequestCount, (count) => {
  if (count > 0) {
    if (active) {
      return
    }

    active = true
    clearTimers()
    showTimer = setTimeout(() => {
      visible.value = true
    }, 120)
    return
  }

  active = false
  clearTimers()
  hideTimer = setTimeout(() => {
    visible.value = false
  }, 260)
})

onBeforeUnmount(clearTimers)
</script>

<template>
  <Transition name="loading-bar">
    <div v-if="visible" class="loading-bar" role="progressbar" aria-label="正在加载">
      <span class="loading-bar-track" />
    </div>
  </Transition>
</template>
