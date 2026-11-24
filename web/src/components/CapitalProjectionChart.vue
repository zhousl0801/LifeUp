<script setup lang="ts">
import { computed, ref } from 'vue'
import type { EChartsOption } from 'echarts'
import type { CapitalProjectionPoint } from '../types/capital'
import { useEChart } from '../composables/useEChart'
import { buildCapitalChartOption, buildMonthlyChartOption, monthlySummary } from '../utils/capitalChart'
import { MaskedMoney, formatMoney, formatSignedMoney } from '../utils/format'

const props = defineProps<{
  points: CapitalProjectionPoint[]
  targetAmount: number | null
  view: 'capital' | 'monthly'
  amountsMasked: boolean
  currentTotal: number
  currentMonth: string
}>()

const emit = defineEmits<{
  'update:view': [view: 'capital' | 'monthly']
}>()

const container = ref<HTMLElement | null>(null)
const summary = computed(() => monthlySummary(props.points))
const title = computed(() => (props.view === 'monthly' ? '每月积累' : '积累曲线'))
const chartLabel = computed(() => props.view === 'monthly'
  ? '每月积累预测图，展示净积累和投资收益'
  : '资本积累预测图，展示净积累、投资收益和目标线')

function buildOption(): EChartsOption {
  const option = props.view === 'monthly'
    ? buildMonthlyChartOption({
        points: props.points,
        targetAmount: props.targetAmount,
        currentTotal: props.currentTotal,
        currentMonth: props.currentMonth,
        amountsMasked: props.amountsMasked
      })
    : buildCapitalChartOption({
        points: props.points,
        targetAmount: props.targetAmount,
        currentTotal: props.currentTotal,
        currentMonth: props.currentMonth,
        amountsMasked: props.amountsMasked
      })

  return option as EChartsOption
}

function setView(view: 'capital' | 'monthly'): void {
  emit('update:view', view)
}

function masked(value: number): string {
  return props.amountsMasked ? MaskedMoney : formatSignedMoney(value)
}

useEChart(container, buildOption, {
  watchSources: () => [
    props.points,
    props.points.length,
    props.targetAmount,
    props.view,
    props.amountsMasked,
    props.currentTotal,
    props.currentMonth
  ],
  flush: 'post',
  resizeOnUpdate: true
})
</script>

<template>
  <section class="capital-card">
    <div class="card-head">
      <div class="title-row">
        <span class="card-title">{{ title }}</span>
        <span class="forecast-tag">预测</span>
      </div>
      <div class="chips" role="tablist" aria-label="积累图表视图">
        <button
          type="button"
          class="chip"
          :class="{ on: view === 'capital' }"
          role="tab"
          :aria-selected="view === 'capital'"
          @click="setView('capital')"
        >
          资本曲线
        </button>
        <button
          type="button"
          class="chip"
          :class="{ on: view === 'monthly' }"
          role="tab"
          :aria-selected="view === 'monthly'"
          @click="setView('monthly')"
        >
          每月积累
        </button>
      </div>
    </div>

    <div v-if="view === 'monthly'" class="chart-summary">
      <span>首年月均</span>
      <b>{{ masked(summary.average) }}</b>
      <span>其中收益 {{ amountsMasked ? MaskedMoney : formatMoney(summary.investment) }}</span>
    </div>

    <div ref="container" class="capital-chart" :class="{ 'is-monthly': view === 'monthly' }" role="img" :aria-label="chartLabel" />

    <div class="legend">
      <span><i class="net"></i>净积累</span>
      <span><i class="return"></i>投资收益</span>
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

.chips {
  display: flex;
  gap: 6px;
}

.chip {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  padding: 6px 10px;
  border: 0;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 500;
}

.chip.on {
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-weight: 600;
}

.chart-summary {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin: 2px 0 4px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.chart-summary b {
  color: var(--color-text-primary);
  font-size: 15px;
  font-variant-numeric: tabular-nums;
}

.capital-chart {
  width: 100%;
  height: 160px;
}

.capital-chart.is-monthly {
  height: 142px;
}

.legend {
  display: flex;
  gap: 14px;
  margin-top: 8px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.legend i {
  display: inline-block;
  width: 8px;
  height: 8px;
  margin-right: 5px;
  border-radius: 2px;
}

.legend .net {
  background: var(--color-brand);
}

.legend .return {
  background: var(--color-chart-property);
}
</style>
