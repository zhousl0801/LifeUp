<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ECharts } from 'echarts/core'
import type { EChartsOption } from 'echarts'
import { useEChart } from '../composables/useEChart'
import { isReducedMotion } from '../composables/useReducedMotion'
import { formatMoney } from '../utils/format'
import { chartTheme } from '../utils/chartTheme'

export interface DonutSlice {
  category: string
  amount: number
  share: number
  color: string
}

const props = defineProps<{
  slices: DonutSlice[]
  total: number
  title: string
  activeCategory?: string | null
}>()

const emit = defineEmits<{
  select: [category: string]
}>()

const container = ref<HTMLElement | null>(null)

const displaySlices = computed(() => {
  if (props.slices.length <= 6) {
    return props.slices
  }

  const head = props.slices.slice(0, 5)
  const rest = props.slices.slice(5)
  const restAmount = rest.reduce((sum, item) => sum + item.amount, 0)
  const restShare = rest.reduce((sum, item) => sum + item.share, 0)

  return [
    ...head,
    {
      category: '其余分类',
      amount: restAmount,
      share: Number(restShare.toFixed(2)),
      color: '#94A3B8'
    }
  ]
})

const activeSlice = computed(() =>
  props.activeCategory
    ? props.slices.find((slice) => slice.category === props.activeCategory) ?? null
    : null
)

function buildOption(): EChartsOption {
  const theme = chartTheme()
  const animate = !isReducedMotion()

  return {
    animation: animate,
    animationDuration: 220,
    animationEasing: 'cubicOut',
    tooltip: { show: false },
    series: [
      {
        type: 'pie',
        radius: ['62%', '88%'],
        center: ['50%', '50%'],
        avoidLabelOverlap: false,
        startAngle: 90,
        padAngle: 2,
        itemStyle: {
          borderRadius: 5,
          borderColor: theme.tooltipBg,
          borderWidth: 1
        },
        label: { show: false },
        emphasis: {
          scale: true,
          scaleSize: 4
        },
        data: displaySlices.value.map((slice) => ({
          name: slice.category,
          value: slice.amount,
          itemStyle: {
            color: slice.color,
            opacity: props.activeCategory && slice.category !== props.activeCategory ? 0.35 : 1
          }
        }))
      }
    ]
  }
}

function handleInit(chart: ECharts): void {
  chart.on('click', (params: { name?: string }) => {
    if (!params.name || params.name === '其余分类') return
    emit('select', params.name)
  })
}

useEChart(container, buildOption, {
  watchSources: () => [displaySlices.value, props.activeCategory],
  onInit: handleInit
})
</script>

<template>
  <div class="donut-layout">
    <div class="donut-stage">
      <div
        ref="container"
        class="donut-canvas"
        role="img"
        :aria-label="`${title}构成环形图，合计 ${formatMoney(total)} 元`"
      />
      <div class="donut-center">
        <span>{{ activeSlice ? activeSlice.category : title }}</span>
        <b>¥{{ formatMoney(activeSlice ? activeSlice.amount : total) }}</b>
        <small v-if="activeSlice">{{ activeSlice.share.toFixed(1) }}%</small>
      </div>
    </div>

    <ul class="donut-legend">
      <li v-for="slice in displaySlices.slice(0, 5)" :key="slice.category">
        <button
          type="button"
          class="legend-item"
          :class="{ active: activeCategory === slice.category }"
          :disabled="slice.category === '其余分类'"
          @click="slice.category !== '其余分类' && emit('select', slice.category)"
        >
          <i class="legend-dot" :style="{ background: slice.color }" />
          <span class="legend-name">{{ slice.category }}</span>
          <span class="legend-share">{{ slice.share.toFixed(1) }}%</span>
        </button>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.donut-layout {
  display: grid;
  grid-template-columns: 146px minmax(0, 1fr);
  align-items: center;
  gap: 14px;
  margin-bottom: 14px;
}

.donut-stage {
  position: relative;
  width: 146px;
  height: 146px;
}

.donut-canvas {
  width: 100%;
  height: 100%;
}

.donut-center {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 20px;
  pointer-events: none;
  text-align: center;
}

.donut-center span {
  max-width: 100%;
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: 0.6875rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.donut-center b {
  margin-top: 2px;
  font-size: 0.875rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.donut-center small {
  margin-top: 1px;
  color: var(--color-brand);
  font-size: 0.6875rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.donut-legend {
  display: grid;
  gap: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.legend-item {
  display: grid;
  grid-template-columns: 8px minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 5px 8px;
  border: 0;
  border-radius: 10px;
  background: transparent;
  color: inherit;
  font-size: var(--text-caption);
  text-align: left;
  transition: background var(--motion-fast) ease;
}

.legend-item:hover:not(:disabled),
.legend-item.active {
  background: var(--color-bg-subtle);
}

.legend-item.active .legend-name {
  color: var(--color-brand);
  font-weight: 700;
}

.legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.legend-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 500;
}

.legend-share {
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}

@media (max-width: 350px) {
  .donut-layout {
    grid-template-columns: 1fr;
    justify-items: center;
  }

  .donut-legend {
    width: 100%;
  }
}
</style>
