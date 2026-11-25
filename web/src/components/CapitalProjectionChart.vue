<script setup lang="ts">
import { computed, ref, useTemplateRef } from 'vue'
import type { EChartsOption } from 'echarts'
import type { CapitalProjectionPoint } from '../types/capital'
import { useEChart } from '../composables/useEChart'
import { useSlidingIndicator } from '../composables/useSlidingIndicator'
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
const chipsRef = useTemplateRef<HTMLElement>('chips')
const { ready: chipPillReady, animated: chipPillAnimated, indicatorStyle: chipPillStyle } = useSlidingIndicator(
  chipsRef,
  () => props.view,
  { activeSelector: '.on' }
)
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
      <div ref="chips" class="chips" :class="{ 'has-pill': chipPillReady }" role="tablist" aria-label="积累图表视图">
        <span
          v-if="chipPillReady"
          class="chips-pill"
          :class="{ 'is-animated': chipPillAnimated }"
          :style="chipPillStyle"
          aria-hidden="true"
        />
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

    <div class="chart-stage" :key="view">
      <div v-if="view === 'monthly'" class="chart-summary">
        <span>首年月均</span>
        <b>{{ masked(summary.average) }}</b>
        <span>其中收益 {{ amountsMasked ? MaskedMoney : formatMoney(summary.investment) }}</span>
      </div>
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
  position: relative;
  display: inline-grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 2px;
  padding: 2px;
  border-radius: var(--radius-pill);
  background: var(--color-bg-subtle);
}

.chips-pill {
  position: absolute;
  top: 2px;
  bottom: 2px;
  left: 0;
  border-radius: var(--radius-pill);
  background: var(--color-brand-soft);
  transform-origin: left center;
  pointer-events: none;
}

.chips-pill.is-animated {
  transition: transform var(--motion-segment) var(--ease-move);
}

.chip {
  position: relative;
  z-index: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 40px;
  padding: 6px 10px;
  border: 0;
  border-radius: var(--radius-pill);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
  transition: color var(--motion-feedback) var(--ease-move);
}

.chip.on {
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-weight: 600;
}

.chips.has-pill .chip.on {
  background: transparent;
}

.chart-stage {
  animation: motion-feedback-in var(--motion-feedback) var(--ease-enter);
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
  height: 160px;
}

@media (prefers-reduced-motion: reduce) {
  .chips-pill.is-animated {
    transition: none;
  }

  .chart-stage {
    animation-duration: var(--motion-reduced-fade);
  }
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
