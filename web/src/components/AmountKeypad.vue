<script setup lang="ts">
import { applyAmountKey, type AmountKey } from '../utils/amount'

const props = defineProps<{
  modelValue: string
  disabled: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

function press(key: AmountKey): void {
  if (props.disabled) {
    return
  }

  emit('update:modelValue', applyAmountKey(props.modelValue, key))
}

</script>

<template>
  <div class="keypad" role="group" aria-label="金额键盘">
    <button v-for="key in ['7', '8', '9', '+', '4', '5', '6', '-', '1', '2', '3'] as const"
      :key="key" type="button" :disabled="disabled"
      :aria-label="key === '+' ? '加' : key === '-' ? '减' : undefined"
      @click="press(key)">{{ key === '-' ? '−' : key }}</button>
    <button type="button" :disabled="disabled" aria-label="删除一位" @click="press('back')">⌫</button>
    <button type="button" :disabled="disabled" aria-label="小数点" @click="press('.')">.</button>
    <button type="button" class="key-zero" :disabled="disabled" @click="press('0')">0</button>
  </div>
</template>

<style scoped>
.keypad {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 4px;
}

.keypad button {
  min-height: clamp(44px, 6dvh, 52px);
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  font-size: 1.25rem;
  font-variant-numeric: tabular-nums;
}

.keypad button:active:not(:disabled) {
  background: var(--color-brand-soft);
}

.keypad button:disabled {
  color: var(--color-text-disabled);
}

.key-zero {
  grid-column: span 2;
}
</style>
