<script setup lang="ts">
import { ref } from 'vue'
import type { EChartsOption } from 'echarts'
import { useEChart } from '../composables/useEChart'
import { formatMoney } from '../utils/format'
import { chartFontSize, chartTheme } from '../utils/chartTheme'
import type { EntryKind, EntryPeriod, EntryStatsPoint } from '../types/entry'

const props = defineProps<{
  points: EntryStatsPoint[]
  direction: EntryKind
  period: EntryPeriod
  total: number
}>()

const container = ref<HTMLElement | null>(null)

function buildOption(): EChartsOption {
  const theme = chartTheme()
  const values = props.points.map((point) => point[props.direction])
  const color = props.direction === 'income' ? theme.loss : theme.brand

  return {
    animation: false,
    grid: { left: 46, right: 10, top: 12, bottom: 24 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
      axisPointer: { type: 'shadow', shadowStyle: { color: theme.pointerShadow } },
      formatter: (params: unknown) => {
        const first = Array.isArray(params) ? params[0] as { dataIndex?: number } | undefined : undefined
        const index = first?.dataIndex
        const point = index === undefined ? undefined : props.points[index]

        if (!point) {
          return ''
        }

        return `${point.label}<br/>${props.direction === 'income' ? '收入' : '支出'} ¥${formatMoney(point[props.direction])}`
      }
    },
    xAxis: {
      type: 'category',
      data: props.points.map((point) => point.label),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: theme.axis } },
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(12),
        interval: props.period === 'month' ? 4 : 0
      }
    },
    yAxis: {
      type: 'value',
      min: 0,
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(12),
        formatter: (value: number) => value >= 10000 ? `${(value / 10000).toFixed(1)}万` : String(value)
      },
      splitLine: { lineStyle: { color: theme.grid } }
    },
    series: [
      {
        name: props.direction === 'income' ? '收入' : '支出',
        type: 'bar',
        data: values,
        barMaxWidth: props.period === 'year' ? 16 : 8,
        barCategoryGap: '35%',
        itemStyle: {
          color,
          borderRadius: [4, 4, 0, 0]
        },
        emphasis: {
          itemStyle: { color }
        }
      }
    ]
  }
}

useEChart(container, buildOption, {
  watchSources: () => [props.points, props.points.length, props.direction, props.period]
})
</script>

<template>
  <div
    ref="container"
    class="stats-chart"
    role="img"
    :aria-label="`${direction === 'income' ? '收入' : '支出'}趋势图，合计 ${formatMoney(total)} 元`"
  />
</template>

<style scoped>
.stats-chart {
  width: 100%;
  height: 184px;
}
</style>
