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
  --glossy-light: 1;
  --sync-stroke: var(--color-btn-soft-stroke);
  --sync-glow: var(--color-btn-soft-glow);
  position: relative;
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex-shrink: 0;
  width: var(--touch-target);
  height: var(--touch-target);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: var(--sync-bg, var(--color-btn-soft-bg));
  color: var(--sync-text, var(--color-btn-soft-fg));
  box-shadow:
    0 2px 6px var(--sync-shadow, var(--color-btn-soft-shadow)),
    0 0 0 1px var(--sync-ring, var(--color-btn-soft-ring));
  transition:
    background var(--motion-fast) ease,
    box-shadow var(--motion-fast) ease,
    transform 220ms cubic-bezier(0.22, 1.35, 0.36, 1);
}

.sync-button::before,
.sync-button::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  pointer-events: none;
}

.sync-button::before {
  padding: 1px;
  background: linear-gradient(
    to bottom,
    rgba(255, 255, 255, var(--sync-stroke)),
    rgba(255, 255, 255, 0)
  );
  -webkit-mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0);
  -webkit-mask-composite: xor;
  mask-composite: exclude;
}

.sync-button::after {
  box-shadow: inset 0 0 5px 1px rgba(255, 255, 255, var(--sync-glow));
  opacity: var(--glossy-light);
  transition: opacity 140ms ease;
}

.on-hero {
  /* 顶部区域不使用 backdrop-filter，避免叠加出毛玻璃雾感；用实色混合替代原半透明。 */
  --sync-bg: color-mix(in srgb, #ffffff 16%, var(--color-hero-bg));
  --sync-text: var(--color-hero-text);
  --sync-hover: rgba(255, 255, 255, 0.22);
  --sync-disabled: var(--color-hero-text-muted);
  --sync-ring: rgba(255, 255, 255, 0.16);
  --sync-shadow: rgba(15, 10, 46, 0.22);
  --sync-stroke: 0.48;
  --sync-glow: 0.18;
}

.sync-button:hover:not(:disabled) {
  background: var(--sync-hover, var(--color-btn-soft-bg-hover));
}

.sync-button:active:not(:disabled) {
  --glossy-light: 0.52;
  transform: scale(0.94);
}

.sync-button:disabled {
  color: var(--sync-disabled, var(--color-text-disabled));
}

.app-icon {
  position: relative;
  width: 20px;
  height: 20px;
}

@media (prefers-reduced-motion: no-preference) {
  .spinning {
    animation: spin .9s linear infinite;
  }
}

@media (prefers-reduced-motion: reduce) {
  .sync-button:active:not(:disabled) {
    transform: none;
  }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
