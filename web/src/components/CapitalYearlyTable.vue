<script setup lang="ts">
import { computed, ref } from 'vue'
import type { CapitalYearRow } from '../types/capital'
import { formatMonthLabel } from '../utils/capital'
import { formatMaskedMoney, formatMoney, formatSignedMoney } from '../utils/format'

const props = defineProps<{
  years: CapitalYearRow[]
  etaMonth: string | null
  targetAmount: number | null
  amountsMasked: boolean
}>()

const expanded = ref<number[]>([])

const rows = computed<CapitalYearRow[]>(() => {
  const visible = props.years.slice(0, 10)
  const goalYear = props.etaMonth ? Number(props.etaMonth.slice(0, 4)) : null
  const hasGoal = visible.some((row) => row.isGoalYear)
  const lastYear = visible[visible.length - 1]?.year ?? 0

  if (props.targetAmount !== null && goalYear !== null && !hasGoal && goalYear > lastYear) {
    return [
      ...visible,
      {
        year: goalYear,
        yearEndCapital: props.targetAmount,
        netSavings: 0,
        providentDeposit: 0,
        return: 0,
        total: 0,
        isGoalYear: true
      }
    ]
  }

  return visible
})

function isExpanded(year: number): boolean {
  return expanded.value.includes(year)
}

function toggle(year: number): void {
  expanded.value = isExpanded(year)
    ? expanded.value.filter((item) => item !== year)
    : [...expanded.value, year]
}

function amount(value: number): string {
  return props.amountsMasked ? formatMaskedMoney(value) : formatMoney(value)
}

function signed(value: number): string {
  return props.amountsMasked ? formatMaskedMoney(value) : formatSignedMoney(value)
}
</script>

<template>
  <section class="capital-card">
    <div class="card-head">
      <div class="title-row">
        <span class="card-title">每年积累</span>
        <span class="forecast-tag">预测</span>
      </div>
    </div>

    <div class="rows">
      <div v-for="row in rows" :key="row.year" class="year-row" :class="{ goal: row.isGoalYear }">
        <button
          type="button"
          class="row-main"
          :aria-expanded="isExpanded(row.year)"
          @click="toggle(row.year)"
        >
          <span class="year">{{ row.year }}</span>
          <span class="cap">
            {{ amount(row.yearEndCapital) }}
            <span v-if="row.isGoalYear" class="badge">达成</span>
          </span>
          <span class="delta">{{ row.isGoalYear && row.total === 0 ? '—' : signed(row.total) }}</span>
          <span class="chev" aria-hidden="true">{{ isExpanded(row.year) ? '⌃' : '⌄' }}</span>
        </button>

        <div v-if="isExpanded(row.year)" class="details">
          <span>净储蓄 <b>{{ amount(row.netSavings) }}</b></span>
          <span>公积金缴存 <b>{{ amount(row.providentDeposit) }}</b></span>
          <span>投资收益 <b>{{ amount(row.return) }}</b></span>
        </div>
      </div>
    </div>

    <p class="note">分项四舍五入，合计可能有 0.01 差异；净储蓄含年终奖。</p>
    <p v-if="etaMonth" class="goal-note">预计 {{ formatMonthLabel(etaMonth) }}达成</p>
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
  margin-bottom: 6px;
}

.title-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.card-title {
  font-size: var(--text-subhead);
  font-weight: 600;
  letter-spacing: -0.01em;
}

.forecast-tag {
  padding: 1px 6px;
  border-radius: var(--radius-tag);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 10px;
  font-weight: 600;
}

.rows {
  margin-top: 2px;
}

.year-row {
  border-top: 1px solid var(--color-border-subtle);
}

.year-row:first-child {
  border-top: 0;
}

.year-row.goal {
  margin: 2px 0;
  border-top: 0;
  border-radius: var(--radius-control);
  background: var(--color-bg-subtle);
  box-shadow: inset 3px 0 0 var(--color-accent-mint-strong);
}

.row-main {
  display: grid;
  grid-template-columns: 48px 1fr auto 18px;
  gap: 8px;
  align-items: center;
  width: 100%;
  min-height: 44px;
  padding: 6px 4px;
  border: 0;
  background: none;
  color: var(--color-text-primary);
  text-align: left;
}

.year {
  color: var(--color-text-secondary);
  font-size: 12px;
  font-variant-numeric: tabular-nums;
}

.cap {
  font-size: 14px;
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}

.delta {
  color: var(--color-finance-gain);
  font-size: 12px;
  font-variant-numeric: tabular-nums;
  font-weight: 500;
  text-align: right;
  white-space: nowrap;
}

.badge {
  display: inline-block;
  margin-left: 6px;
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  background: var(--color-accent-mint);
  color: var(--color-on-accent-mint);
  font-size: 10px;
  font-weight: 600;
}

.chev {
  color: var(--color-text-secondary);
  font-size: 12px;
  text-align: center;
}

.details {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 14px;
  padding: 0 4px 9px 52px;
  color: var(--color-text-secondary);
  font-size: 11px;
}

.details b {
  margin-left: 3px;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.note,
.goal-note {
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 11px;
}

.goal-note {
  color: var(--color-brand);
}
</style>
