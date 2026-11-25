<script setup lang="ts">
import { useAmountPrivacy } from '../composables/useAmountPrivacy'
import AppIcon from './AppIcon.vue'

const { amountsMasked } = useAmountPrivacy()
</script>

<template>
  <button
    type="button"
    class="amount-visibility"
    :class="{ 'is-masked': amountsMasked }"
    :aria-label="amountsMasked ? '显示所有页面金额' : '隐藏所有页面金额'"
    @click="amountsMasked = !amountsMasked"
  >
    <span class="visibility-slot" aria-hidden="true">
      <AppIcon class="icon-visible" name="eye" />
      <AppIcon class="icon-masked" name="eye-off" />
    </span>
  </button>
</template>

<style scoped>
.amount-visibility {
  display: grid;
  flex: none;
  place-items: center;
  width: var(--touch-target);
  height: var(--touch-target);
  margin-block: -8px;
  margin-inline-start: auto;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: var(--color-hero-text-secondary);
  transition:
    background-color var(--motion-feedback) var(--ease-move),
    color var(--motion-feedback) var(--ease-move),
    transform var(--motion-press-out) var(--ease-settle);
}

@media (hover: hover) and (pointer: fine) {
  .amount-visibility:hover {
    background: color-mix(in srgb, currentColor 12%, transparent);
  }
}

.amount-visibility:active {
  transform: scale(0.92);
  transition-duration: var(--motion-press-in);
}

.visibility-slot {
  display: grid;
  place-items: center;
  width: 18px;
  height: 18px;
}

.app-icon {
  grid-area: 1 / 1;
  width: 18px;
  height: 18px;
  transition: opacity var(--motion-feedback) var(--ease-move);
}

.icon-visible {
  opacity: 1;
}

.icon-masked {
  opacity: 0;
}

.amount-visibility.is-masked .icon-visible {
  opacity: 0;
}

.amount-visibility.is-masked .icon-masked {
  opacity: 1;
}

@media (prefers-reduced-motion: reduce) {
  .amount-visibility {
    transition: background-color var(--motion-reduced-fade) var(--ease-linear);
  }

  .amount-visibility:active {
    transform: none;
  }

  .app-icon {
    transition-duration: var(--motion-reduced-fade);
  }
}
</style>
