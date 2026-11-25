<script setup lang="ts">
import AppIcon from './AppIcon.vue'

defineEmits<{ back: [] }>()
</script>

<template>
  <button class="back-button" type="button" aria-label="返回" @click="$emit('back')">
    <AppIcon name="chevron-back" />
  </button>
</template>

<style scoped>
.back-button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  width: var(--touch-target);
  height: var(--touch-target);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
  color: var(--color-text-primary);
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-settle);
}

@media (hover: hover) and (pointer: fine) {
  .back-button:hover {
    background: var(--color-bg-hover);
  }
}

.back-button:active {
  transform: scale(0.95);
  transition-duration: var(--motion-press-in);
}

@media (prefers-reduced-motion: reduce) {
  .back-button {
    transition: background-color var(--motion-reduced-fade) var(--ease-linear);
  }

  .back-button:active {
    transform: none;
  }
}

.back-button :deep(.app-icon) {
  width: 20px;
  height: 20px;
}
</style>
