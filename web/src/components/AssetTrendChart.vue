<script setup lang="ts">
import { computed, ref } from 'vue'
import type { EChartsOption } from 'echarts'
import { useEChart } from '../composables/useEChart'
import { todayInBusinessTimeZone } from '../utils/format'
import { buildAssetTrend, buildTrendOption, buildTrendPlaceholderOption, trendAmountLabel } from '../utils/trend'
import type { AssetTrend, AssetTrendPoint, TrendRange } from '../types/trend'

const props = defineProps<{
  trend: AssetTrend | null
  placeholderRange?: TrendRange
  placeholderEndDate?: string
  variant?: 'default' | 'hero'
  amountsMasked?: boolean
}>()

const emit = defineEmits<{ select: [point: AssetTrendPoint] }>()

const container = ref<HTMLElement | null>(null)
const series = computed(() => props.trend ? buildAssetTrend(props.trend) : null)
const chartLabel = computed(() => {
  if (!props.trend || !series.value) {
    return '正在读取资产趋势图'
  }

  const latest = series.value.latest
  const label = latest?.source.totalAssets == null ? '已记录资产' : '总资产'
  return `资产趋势图，${props.trend.from} 至 ${props.trend.to}；最新${label} ${trendAmountLabel(latest, props.amountsMasked)}`
})

useEChart(
  container,
  () => {
    if (!props.trend || !series.value) {
      return buildTrendPlaceholderOption(
        props.placeholderRange ?? 'week',
        props.placeholderEndDate ?? todayInBusinessTimeZone(),
        props.variant ?? 'default'
      ) as EChartsOption
    }

    return buildTrendOption(props.trend, series.value, props.variant ?? 'default', props.amountsMasked) as EChartsOption
  },
  {
    watchSources: () => [
      props.trend,
      props.trend?.points,
      props.trend?.points.length,
      props.placeholderRange,
      props.placeholderEndDate,
      props.variant,
      props.amountsMasked
    ],
    resizeOnUpdate: true,
    setOptionOpts: { notMerge: false, replaceMerge: ['yAxis'] },
    onInit(chart) {
      chart.on('updateAxisPointer', (event: unknown) => {
        if (!props.trend) return

        const payload = event as { axesInfo?: { axisDim: string; value: number | string }[] }
        const value = payload.axesInfo?.find((axis) => axis.axisDim === 'x')?.value
        const point = typeof value === 'number' && Number.isInteger(value)
          ? props.trend.points[value]
          : props.trend.points.find((item) => item.date === value)

        if (point) emit('select', point)
      })
    }
  }
)
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
