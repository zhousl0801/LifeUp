<script setup lang="ts">
withDefaults(defineProps<{
  loading?: boolean
  disabled?: boolean
  type?: 'button' | 'submit' | 'reset'
  loadingLabel?: string
}>(), {
  loading: false,
  disabled: false,
  type: 'button',
  loadingLabel: '处理中…'
})
</script>

<template>
  <button
    :type="type"
    class="loading-button"
    :disabled="disabled || loading"
    :aria-busy="loading ? 'true' : undefined"
  >
    <span v-if="loading" class="loading-button-spinner" aria-hidden="true" />
    <span class="loading-button-label">
      <slot v-if="!loading" />
      <template v-else>{{ loadingLabel }}</template>
    </span>
  </button>
</template>

<style scoped>
.loading-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.loading-button-label {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-width: 0;
}

.loading-button-spinner {
  flex: 0 0 auto;
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: loading-button-spin 0.7s linear infinite;
}

@keyframes loading-button-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .loading-button-spinner {
    animation-duration: 1.6s;
  }
}
</style>
