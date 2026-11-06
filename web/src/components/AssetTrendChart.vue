<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { ECharts } from 'echarts/core'
import type { EChartsOption } from 'echarts'
import { loadChartRuntime } from '../utils/chartRuntime'
import { assetLabels, buildScopedTrend, buildTrendOption } from '../utils/trend'
import { resolvedTheme } from '../utils/theme'
import type { AssetTrend, AssetTrendPoint } from '../types/trend'

const props = defineProps<{
  trend: AssetTrend
  variant?: 'default' | 'hero'
}>()

const emit = defineEmits<{ select: [point: AssetTrendPoint] }>()

const container = ref<HTMLElement | null>(null)
const scoped = computed(() => buildScopedTrend(props.trend))
const chartLabel = computed(() => {
  const latest = scoped.value.latest
  const latestText = latest?.value == null ? '最新依据不足' : `最新同口径资产 ${latest.value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} 元`
  const types = scoped.value.baselineTypes.map((key) => assetLabels[key]).join('、')
  return `资产趋势图，${props.trend.from} 至 ${props.trend.to}；${latestText}${types ? `；包含${types}` : ''}`
})
let chart: ECharts | null = null
let disposed = false

function resize(): void {
  chart?.resize()
}

function render(): void {
  chart?.setOption(
    buildTrendOption(props.trend, scoped.value, props.variant ?? 'default') as EChartsOption,
    { notMerge: true }
  )
}

onMounted(() => {
  window.addEventListener('resize', resize)

  void loadChartRuntime().then((echarts) => {
    if (disposed || !container.value) {
      return
    }

    chart = echarts.init(container.value, undefined, { renderer: 'canvas' })
    render()
    chart.on('updateAxisPointer', (event: unknown) => {
      const payload = event as { axesInfo?: { axisDim: string; value: number | string }[] }
      const value = payload.axesInfo?.find((axis) => axis.axisDim === 'x')?.value
      const point = typeof value === 'number' && Number.isInteger(value)
        ? props.trend.points[value]
        : props.trend.points.find((item) => item.date === value)

      if (point) emit('select', point)
    })
  })
})

watch(
  () => props.trend,
  () => {
    render()
  },
  { deep: true }
)

watch(
  () => props.variant,
  () => render()
)

watch(resolvedTheme, () => render())

onBeforeUnmount(() => {
  disposed = true
  window.removeEventListener('resize', resize)
  chart?.dispose()
  chart = null
})
</script>

<template>
  <div
    ref="container"
    class="trend-chart"
    :class="{ 'is-hero': variant === 'hero' }"
    role="img"
    :aria-label="chartLabel"
  />
</template>

<style scoped>
.trend-chart {
  width: 100%;
  height: 120px;
}

.trend-chart.is-hero {
  height: 140px;
}
</style>
