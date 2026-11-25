<script setup lang="ts">
import { shallowRef, useTemplateRef, watch } from 'vue'

const props = withDefaults(defineProps<{
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

const buttonRef = useTemplateRef<HTMLButtonElement>('button')
const lockedWidth = shallowRef<string | undefined>(undefined)

watch(
  () => props.loading,
  (isLoading) => {
    if (isLoading) {
      const width = buttonRef.value?.getBoundingClientRect().width ?? 0
      lockedWidth.value = width > 0 ? `${Math.ceil(width)}px` : undefined
    } else {
      lockedWidth.value = undefined
    }
  }
)
</script>

<template>
  <button
    ref="button"
    :type="type"
    class="loading-button"
    :class="{ 'is-loading': loading }"
    :style="lockedWidth ? { minWidth: lockedWidth } : undefined"
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
  transition: opacity var(--motion-feedback) var(--ease-move);
}

.loading-button.is-loading .loading-button-label {
  animation: loading-button-label-in var(--motion-feedback) var(--ease-enter) both;
}

.loading-button-spinner {
  flex: 0 0 auto;
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation:
    loading-button-fade var(--motion-feedback) var(--ease-enter) var(--motion-busy-delay) both,
    loading-button-spin var(--motion-busy-cycle) var(--ease-linear) var(--motion-busy-delay) infinite;
}

@keyframes loading-button-fade {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

@keyframes loading-button-label-in {
  from {
    opacity: 0.72;
  }
  to {
    opacity: 1;
  }
}

@keyframes loading-button-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .loading-button.is-loading .loading-button-label {
    animation: none;
  }

  .loading-button-spinner {
    border-right-color: color-mix(in srgb, currentColor 35%, transparent);
    animation: loading-button-fade var(--motion-reduced-fade) var(--ease-linear) both;
  }
}
</style>
