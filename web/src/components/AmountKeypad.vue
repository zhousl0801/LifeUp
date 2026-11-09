<script setup lang="ts">
import { ref } from 'vue'
import { applyAmountKey, type AmountKey } from '../utils/amount'

const props = defineProps<{
  modelValue: string
  disabled: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const operations = ref<'add' | 'multiply' | null>(null)

function press(key: AmountKey): void {
  if (props.disabled) {
    return
  }

  operations.value = null
  emit('update:modelValue', applyAmountKey(props.modelValue, key))
}

</script>

<template>
  <div class="keypad" role="group" aria-label="金额键盘">
    <button v-for="key in ['7', '8', '9'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <button type="button" :disabled="disabled" aria-label="删除一位" @click="press('back')">⌫</button>
    <button v-for="key in ['4', '5', '6'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <button type="button" class="operator" :disabled="disabled" aria-label="选择加减" :aria-expanded="operations === 'add'"
      @click="operations = operations === 'add' ? null : 'add'">＋ / −</button>
    <button v-for="key in ['1', '2', '3'] as const" :key="key" type="button" :disabled="disabled" @click="press(key)">{{ key }}</button>
    <button type="button" class="operator" :disabled="disabled" aria-label="选择乘除" :aria-expanded="operations === 'multiply'"
      @click="operations = operations === 'multiply' ? null : 'multiply'">× / ÷</button>
    <button type="button" :disabled="disabled" aria-label="小数点" @click="press('.')">.</button>
    <button type="button" :disabled="disabled" @click="press('0')">0</button>
    <button type="button" :disabled="disabled" @click="press('00')">00</button>
    <button type="button" class="operator" :disabled="disabled" aria-label="计算结果" @click="press('=')">＝</button>
    <div v-if="operations && !disabled" class="operator-options" role="group" aria-label="运算符">
      <template v-if="operations === 'add'">
        <button type="button" aria-label="加" @click="press('+')">＋</button>
        <button type="button" aria-label="减" @click="press('-')">−</button>
      </template>
      <template v-else>
        <button type="button" aria-label="乘" @click="press('*')">×</button>
        <button type="button" aria-label="除" @click="press('/')">÷</button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.keypad {
  position: relative;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 4px;
}

.keypad button {
  min-height: clamp(44px, 6dvh, 52px);
  border: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-surface);
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

.operator { color: var(--color-brand); }

.operator-options {
  position: absolute;
  right: 0;
  bottom: calc(100% + 6px);
  display: flex;
  gap: 4px;
  padding: 4px;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  box-shadow: var(--shadow-card);
}

.operator-options button { min-width: 44px; }
</style>
