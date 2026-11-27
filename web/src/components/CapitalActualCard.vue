<script setup lang="ts">
import { computed } from 'vue'
import type { CapitalActualMonth } from '../types/capital'
import { amountTone, MaskedMoney, formatMoney, formatSignedMoney } from '../utils/format'

const props = defineProps<{
  actual: CapitalActualMonth
  amountsMasked: boolean
  isFireMode?: boolean
  fireBaselineMonthlyExpense?: number | null
}>()

const emit = defineEmits<{
  'open-change': []
}>()

const difference = computed(() => {
  if (props.actual.comparableChange === null) {
    return null
  }

  return props.actual.comparableChange - props.actual.expectedNetDeposit
})

const actualText = computed(() => {
  if (props.actual.comparableChange === null) {
    return '—'
  }

  return props.amountsMasked
    ? MaskedMoney
    : formatSignedMoney(props.actual.comparableChange)
})

const expectedText = computed(() => props.amountsMasked
  ? MaskedMoney
  : formatSignedMoney(props.actual.expectedNetDeposit))

const diffText = computed(() => {
  if (difference.value === null) {
    return props.actual.note ?? '本月行情或资产依据不足'
  }

  const direction = difference.value >= 0 ? '多' : '少'
  const amount = props.amountsMasked ? '••••' : formatMoney(Math.abs(difference.value))

  return `比假设${direction} ${amount}，${props.actual.note ?? '主要来自本月投资市值变动'}`
})
const fireExpense = computed(() => {
  const expense = props.actual.actualExpense
  const baseline = props.fireBaselineMonthlyExpense
  if (!props.isFireMode || expense == null || baseline == null || baseline <= 0) return null

  const amount = (value: number) => props.amountsMasked ? '••••' : `¥${formatMoney(value)}`
  const above = expense > baseline
  const baselineText = props.amountsMasked ? amount(baseline) : `${amount(baseline)}/月`
  const percent = Number((expense / baseline * 100).toFixed(1))
  return {
    above,
    text: `本月支出 ${amount(expense)} · ${above
      ? `高于 FIRE 基准 ${baselineText}`
      : `占 FIRE 基准（${baselineText}）的 ${percent}%`}`
  }
})
</script>

<template>
  <section class="capital-card">
    <div class="card-head">
      <span class="card-title">本月实际</span>
      <button type="button" class="link" @click="emit('open-change')">本月变化 ›</button>
    </div>

    <div class="actual">
      <div>
        <span class="k">实际积累</span>
        <b class="v" :class="amountTone(actual.comparableChange)">{{ actualText }}</b>
      </div>
      <div>
        <span class="k">按假设应净积累</span>
        <b class="v flat">{{ expectedText }}</b>
      </div>
      <p class="diff">{{ diffText }}</p>
      <p v-if="fireExpense" class="fire-expense" :class="{ 'is-above': fireExpense.above }">{{ fireExpense.text }}</p>
    </div>
  </section>
</template>

<style scoped>
.capital-card {
  margin-top: 14px;
  padding: 17px;
  border-radius: var(--radius-card);
  background: var(--color-bg-surface);
  box-shadow: var(--shadow-card);
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 8px;
}

.card-title {
  font-size: var(--text-subhead);
  font-weight: 600;
  letter-spacing: -0.01em;
}

.link {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  padding: 4px 0;
  border: 0;
  background: none;
  color: var(--color-brand);
  font-size: 12px;
  font-weight: 500;
}

.actual {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px 14px;
}

.k {
  display: block;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.v {
  display: block;
  margin-top: 2px;
  font-size: 21px;
  font-variant-numeric: tabular-nums;
  font-weight: 700;
  letter-spacing: -0.02em;
}

.v.flat {
  color: var(--color-text-primary);
}

.diff {
  grid-column: 1 / -1;
  margin: 2px 0 0;
  padding-top: 9px;
  border-top: 1px solid var(--color-border-subtle);
  color: var(--color-text-secondary);
  font-size: 12px;
}
.fire-expense {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--color-text-secondary);
  font-size: var(--text-caption);
  font-variant-numeric: tabular-nums;
  line-height: 1.5;
}

.fire-expense.is-above {
  color: var(--color-tint-amber-fg);
}
</style>
