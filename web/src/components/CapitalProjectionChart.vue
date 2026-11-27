<script setup lang="ts">
import { computed, ref, shallowRef, useTemplateRef, watch } from 'vue'
import type { EChartsOption } from 'echarts'
import type { CapitalChartView, CapitalProjectionPoint, FireOverview } from '../types/capital'
import { useEChart } from '../composables/useEChart'
import { useSlidingIndicator } from '../composables/useSlidingIndicator'
import { buildCapitalChartOption, buildCashflowChartOption, buildMonthlyChartOption, monthlySummary } from '../utils/capitalChart'
import { MaskedMoney, formatMoney, formatSignedMoney } from '../utils/format'

const props = defineProps<{
  points: CapitalProjectionPoint[]
  targetAmount: number | null
  view?: CapitalChartView
  fire?: FireOverview | null
  amountsMasked: boolean
  currentTotal: number
  currentMonth: string
}>()

const emit = defineEmits<{
  'update:view': [view: CapitalChartView]
}>()

const fireMode = computed(() => props.fire?.goalMode === 'fire')
const defaultView = computed(() => fireMode.value ? 'cashflow' : 'capital')
const localView = shallowRef<CapitalChartView>()
watch(defaultView, () => { localView.value = undefined })
const view = computed(() => {
  const requested = props.view ?? localView.value
  return requested === undefined || (requested === 'cashflow' && !fireMode.value) ? defaultView.value : requested
})
const tabs = computed<{ value: CapitalChartView; label: string }[]>(() => [
  ...(fireMode.value ? [{ value: 'cashflow' as const, label: '现金流' }] : []),
  { value: 'capital', label: fireMode.value ? '资产曲线' : '资本曲线' },
  { value: 'monthly', label: '每月积累' }
])

const container = ref<HTMLElement | null>(null)
const chipsRef = useTemplateRef<HTMLElement>('chips')
const { ready: chipPillReady, animated: chipPillAnimated, indicatorStyle: chipPillStyle } = useSlidingIndicator(
  chipsRef,
  view,
  { activeSelector: '.on' }
)
const summary = computed(() => monthlySummary(props.points))
const titles: Record<CapitalChartView, string> = { cashflow: '自由现金流', capital: '积累曲线', monthly: '每月积累' }
const labels: Record<CapitalChartView, string> = {
  cashflow: 'FIRE 现金流预测图，比较被动年现金流和预期年支出',
  capital: '资本积累预测图，展示净积累、投资收益和目标线',
  monthly: '每月积累预测图，展示净积累和投资收益'
}

function buildOption(): EChartsOption {
  const input = {
    points: props.points,
    targetAmount: props.targetAmount,
    currentTotal: props.currentTotal,
    currentMonth: props.currentMonth,
    amountsMasked: props.amountsMasked
  }

  if (view.value === 'cashflow' && props.fire) return buildCashflowChartOption(input, props.fire)
  return (view.value === 'monthly' ? buildMonthlyChartOption(input) : buildCapitalChartOption(input)) as EChartsOption
}

function setView(next: CapitalChartView): void {
  localView.value = next
  emit('update:view', next)
}

function masked(value: number): string {
  return props.amountsMasked ? MaskedMoney : formatSignedMoney(value)
}

useEChart(container, buildOption, {
  watchSources: () => [
    props.points,
    props.points.length,
    props.targetAmount,
    view.value,
    props.fire,
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
    <div class="card-head" :class="{ 'is-fire': fireMode }">
      <div class="title-row">
        <span class="card-title">{{ titles[view] }}</span>
        <span class="forecast-tag">预测</span>
      </div>
      <div ref="chips" class="chips" :class="{ 'has-pill': chipPillReady, 'is-fire': fireMode }" role="tablist" aria-label="积累图表视图">
        <span
          v-if="chipPillReady"
          class="chips-pill"
          :class="{ 'is-animated': chipPillAnimated }"
          :style="chipPillStyle"
          aria-hidden="true"
        />
        <button
          v-for="tab in tabs"
          :key="tab.value"
          type="button"
          class="chip"
          :class="{ on: view === tab.value }"
          role="tab"
          :aria-selected="view === tab.value"
          @click="setView(tab.value)"
        >{{ tab.label }}</button>
      </div>
    </div>

    <div class="chart-stage" :key="view">
      <div v-if="view === 'monthly'" class="chart-summary">
        <span>首年月均</span>
        <b>{{ masked(summary.average) }}</b>
        <span>其中收益 {{ amountsMasked ? MaskedMoney : formatMoney(summary.investment) }}</span>
      </div>
    </div>

    <div ref="container" class="capital-chart" :class="{ 'is-monthly': view === 'monthly' }" role="img" :aria-label="labels[view]" />

    <div class="legend">
      <template v-if="view === 'cashflow'">
        <span><i class="passive"></i>被动年现金流</span>
        <span><i class="expense"></i>预期年支出</span>
      </template>
      <template v-else>
        <span><i class="net"></i>净积累</span>
        <span><i class="return"></i>投资收益</span>
      </template>
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

.card-head.is-fire {
  align-items: stretch;
  flex-direction: column;
}

.chips.is-fire {
  grid-template-columns: repeat(3, minmax(0, 1fr));
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
  min-height: var(--touch-target);
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
.legend i.passive,
.legend i.expense {
  width: 16px;
  height: 0;
  border: 0;
  border-top: 2px solid var(--color-tint-emerald-fg);
  border-radius: 0;
  background: transparent;
}

.legend i.expense {
  border-top: 2px dashed var(--color-tint-amber-fg);
}

.capital-card,
:deep(.cashflow-tooltip) {
  font-variant-numeric: tabular-nums;
}
</style>
