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
    <button v-for="key in ['7', '8', '9'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <button type="button" :disabled="disabled" aria-label="删除一位" @click="press('back')">⌫</button>
    <button v-for="key in ['4', '5', '6'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <div class="operator-pair">
      <button type="button" class="operator" :disabled="disabled" aria-label="加" @click="press('+')">＋</button>
      <button type="button" :disabled="disabled" aria-label="减" @click="press('-')">−</button>
    </div>
    <button v-for="key in ['1', '2', '3'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <div class="operator-pair">
      <button type="button" class="operator" :disabled="disabled" aria-label="乘" @click="press('*')">×</button>
      <button type="button" :disabled="disabled" aria-label="除" @click="press('/')">÷</button>
    </div>
    <button type="button" :disabled="disabled" aria-label="小数点" @click="press('.')">.</button>
    <button type="button" :disabled="disabled" @click="press('0')">0</button>
    <button type="button" :disabled="disabled" @click="press('00')">00</button>
    <button type="button" class="operator" :disabled="disabled" aria-label="计算结果" @click="press('=')">＝</button>
  </div>
</template>

<style scoped>
.keypad {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 4px;
}

.keypad button {
  position: relative;
  overflow: hidden;
  min-height: clamp(44px, 6dvh, 52px);
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 1.25rem;
  font-variant-numeric: tabular-nums;
  user-select: none;
  -webkit-user-select: none;
  transition: transform var(--motion-press-out) var(--ease-settle);
}

.keypad button::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  background: var(--color-brand-soft);
  opacity: 0;
  pointer-events: none;
  transition: opacity var(--motion-press-out) var(--ease-settle);
}

@media (hover: hover) and (pointer: fine) {
  .keypad button:hover:not(:disabled)::after {
    opacity: 0.45;
  }
}

.keypad button:active:not(:disabled) {
  transform: scale(0.985);
  transition-duration: var(--motion-press-in);
  transition-timing-function: var(--ease-exit);
}

.keypad button:active:not(:disabled)::after {
  opacity: 1;
  transition-duration: var(--motion-press-in);
}

.keypad button:disabled {
  color: var(--color-text-disabled);
}

.keypad .operator:not(:disabled) { color: var(--color-brand); }

.operator-pair {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  gap: 4px;
}

.operator-pair button {
  padding-inline: 0;
}

@media (prefers-reduced-motion: reduce) {
  .keypad button {
    transition: none;
  }

  .keypad button:active:not(:disabled) {
    transform: none;
  }

  .keypad button::after {
    transition-duration: var(--motion-reduced-fade);
  }
}
</style>
