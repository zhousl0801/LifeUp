<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useHomePreferencesStore } from '../stores/homePreferences'
import AppIcon from './AppIcon.vue'

const { amountsMasked } = storeToRefs(useHomePreferencesStore())
</script>

<template>
  <button
    type="button"
    class="amount-visibility"
    :aria-label="amountsMasked ? '显示资产金额' : '隐藏资产金额'"
    @click="amountsMasked = !amountsMasked"
  >
    <AppIcon :name="amountsMasked ? 'eye-off' : 'eye'" />
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
}

.app-icon {
  width: 18px;
  height: 18px;
}
</style>
