<script setup lang="ts">
import { computed, ref } from 'vue'
import type { EChartsOption } from 'echarts'
import { useEChart } from '../composables/useEChart'
import { formatSignedMoney } from '../utils/format'
import { chartFontSize, chartTheme } from '../utils/chartTheme'
import type { FundReturnPoint } from '../types/fundReturn'

const props = defineProps<{
  points: FundReturnPoint[]
  range: string
}>()

const container = ref<HTMLElement | null>(null)

const chartLabel = computed(() => {
  const latest = [...props.points].reverse().find((point) => point.cumulativeProfit !== null)

  return latest
    ? `投资收益走势图，${props.range}；最新累计收益 ${formatSignedMoney(latest.cumulativeProfit!)} 元；红涨绿跌，柱子方向表示当日盈亏`
    : `投资收益走势图，${props.range}；暂无已披露收益`
})

function buildOption(): EChartsOption {
  const theme = chartTheme()

  return {
    animation: false,
    grid: { left: 48, right: 14, top: 34, bottom: 26 },
    legend: {
      top: 0,
      right: 0,
      itemWidth: 10,
      itemHeight: 8,
      textStyle: { fontSize: chartFontSize(12), color: theme.text }
    },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
      formatter: (params: unknown) => {
        const items = Array.isArray(params) ? params as { dataIndex?: number }[] : []
        const index = items[0]?.dataIndex
        const point = index === undefined ? undefined : props.points[index]

        if (!point) {
          return ''
        }

        const daily = point.dailyProfit === null
          ? point.hasDisclosure ? '当日收益 —（依据不足）' : '当日收益 —（未披露）'
          : `当日收益 ${formatSignedMoney(point.dailyProfit)}`
        const cumulative = point.cumulativeProfit === null
          ? '累计收益 —'
          : `累计收益 ${formatSignedMoney(point.cumulativeProfit)}`
        const coverage = point.isComplete
          ? '全部基金已披露'
          : `覆盖 ${point.coveredPositions}/${point.totalPositions} 个持仓`

        return `${point.date}<br/>${daily}<br/>${cumulative}<br/>${coverage}`
      }
    },
    xAxis: {
      type: 'category',
      data: props.points.map((point) => point.date.slice(5).replace('-', '/')),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: theme.axis } },
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(12),
        interval: axisInterval()
      }
    },
    yAxis: {
      type: 'value',
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(12),
        formatter: (value: number) => Math.abs(value) >= 10000 ? `${(value / 10000).toFixed(1)}万` : String(value)
      },
      splitLine: { lineStyle: { color: theme.grid } }
    },
    dataZoom: [{ type: 'inside' }],
    series: [
      {
        name: '当日收益',
        type: 'bar',
        data: props.points.map((point) => point.dailyProfit === null
          ? null
          : {
              value: point.dailyProfit,
              itemStyle: { color: point.dailyProfit >= 0 ? theme.gain : theme.loss }
            }),
        barMaxWidth: 8
      },
      {
        name: '累计收益',
        type: 'line',
        data: props.points.map((point) => point.cumulativeProfit),
        smooth: false,
        showSymbol: false,
        connectNulls: false,
        lineStyle: { width: 2, color: theme.brand },
        itemStyle: { color: theme.brand }
      }
    ]
  }
}

function axisInterval(): number {
  if (props.range === 'all' || props.points.length > 90) {
    return Math.ceil(props.points.length / 8)
  }

  return props.range === 'month' ? 3 : 6
}

useEChart(container, buildOption, {
  watchSources: () => [props.points, props.points.length, props.range]
})
</script>

<template>
  <div ref="container" class="fund-return-chart" role="img" :aria-label="chartLabel" />
</template>

<style scoped>
.fund-return-chart {
  width: 100%;
  height: 220px;
}
</style>
