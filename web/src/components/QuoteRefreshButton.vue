<script setup lang="ts">
import AppIcon from './AppIcon.vue'

defineProps<{ refreshing: boolean; disabled?: boolean; onHero?: boolean }>()
defineEmits<{ refresh: [] }>()
</script>

<template>
  <button
    type="button"
    class="sync-button"
    :class="{ 'on-hero': onHero }"
    :disabled="disabled || refreshing"
    :aria-label="refreshing ? '正在同步行情' : '同步行情'"
    :title="refreshing ? '正在同步行情' : '同步行情'"
    @click="$emit('refresh')"
  >
    <AppIcon name="refresh" :class="{ spinning: refreshing }" />
  </button>
</template>

<style scoped>
.sync-button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex-shrink: 0;
  width: var(--touch-target);
  height: var(--touch-target);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: var(--sync-bg, var(--color-brand-soft));
  color: var(--sync-text, var(--color-brand));
  transition: background var(--motion-fast) ease, transform var(--motion-fast) ease;
}

.on-hero {
  --sync-bg: var(--color-hero-surface);
  --sync-text: var(--color-hero-text);
  --sync-hover: var(--color-hero-surface-strong);
  --sync-disabled: var(--color-hero-text-muted);
}

.sync-button:hover:not(:disabled) {
  background: var(--sync-hover, var(--color-brand-soft-hover));
}

.sync-button:active:not(:disabled) {
  transform: scale(.94);
}

.sync-button:disabled {
  color: var(--sync-disabled, var(--color-text-disabled));
}

.app-icon {
  width: 20px;
  height: 20px;
}

@media (prefers-reduced-motion: no-preference) {
  .spinning {
    animation: spin .9s linear infinite;
  }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
