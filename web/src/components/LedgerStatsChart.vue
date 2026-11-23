<script setup lang="ts">
import { ref } from 'vue'
import type { ECharts } from 'echarts/core'
import type { EChartsOption } from 'echarts'
import { useEChart } from '../composables/useEChart'
import { formatMoney, sumAmounts } from '../utils/format'
import { chartFontSize, chartTheme } from '../utils/chartTheme'
import type { EntryPeriod, EntryStatsPoint } from '../types/entry'

export type StatsChartMode = 'expense' | 'income' | 'balance'

const props = defineProps<{
  points: EntryStatsPoint[]
  mode: StatsChartMode
  period: EntryPeriod
  total: number
  average?: number
  selectedDate?: string | null
}>()

const emit = defineEmits<{
  selectPoint: [point: EntryStatsPoint]
}>()

const container = ref<HTMLElement | null>(null)

function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

function formatAxisMoney(value: number): string {
  const abs = Math.abs(value)
  if (abs >= 10000) {
    return `${(value / 10000).toFixed(1)}万`
  }
  if (abs >= 1000) {
    return `${(value / 1000).toFixed(abs % 1000 === 0 ? 0 : 1)}k`
  }
  return String(value)
}

function buildOption(): EChartsOption {
  const theme = chartTheme()
  const animate = !prefersReducedMotion()
  const labels = props.points.map((point) => point.label)
  const xInterval = props.period === 'month' ? 4 : 0

  if (props.mode === 'balance') {
    const incomes = props.points.map((point) => point.income)
    const expenses = props.points.map((point) => point.expense)
    const surpluses = props.points.map((point) => sumAmounts([point.income, -point.expense]))

    return {
      animation: animate,
      animationDuration: 280,
      grid: { left: 46, right: 12, top: 28, bottom: 26 },
      legend: {
        top: 0,
        right: 0,
        itemWidth: 10,
        itemHeight: 10,
        icon: 'roundRect',
        textStyle: { color: theme.text, fontSize: chartFontSize(11) }
      },
      tooltip: {
        trigger: 'axis',
        confine: true,
        backgroundColor: theme.tooltipBg,
        borderWidth: 0,
        textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
        axisPointer: { type: 'shadow', shadowStyle: { color: theme.pointerShadow } },
        formatter: (params: unknown) => {
          const first = Array.isArray(params) ? params[0] as { dataIndex?: number } | undefined : undefined
          const point = first?.dataIndex === undefined ? undefined : props.points[first.dataIndex]
          if (!point) return ''
          const net = sumAmounts([point.income, -point.expense])
          const sign = net > 0 ? '+' : net < 0 ? '−' : ''
          return [
            `<strong>${point.label}</strong>`,
            `收入 ¥${formatMoney(point.income)}`,
            `支出 ¥${formatMoney(point.expense)}`,
            `结余 ${sign}¥${formatMoney(Math.abs(net))}`
          ].join('<br/>')
        }
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisTick: { show: false },
        axisLine: { lineStyle: { color: theme.axis } },
        axisLabel: { color: theme.text, fontSize: chartFontSize(11), interval: xInterval }
      },
      yAxis: {
        type: 'value',
        axisLabel: {
          color: theme.text,
          fontSize: chartFontSize(11),
          formatter: formatAxisMoney
        },
        splitLine: { lineStyle: { color: theme.grid } }
      },
      series: [
        {
          name: '收入',
          type: 'bar',
          data: incomes.map((value, index) => ({
            value,
            itemStyle: {
              color: theme.loss,
              opacity: props.selectedDate && props.points[index]?.date !== props.selectedDate ? 0.35 : 0.9,
              borderRadius: [3, 3, 0, 0]
            }
          })),
          barMaxWidth: props.period === 'year' ? 10 : 5,
          barGap: '18%'
        },
        {
          name: '支出',
          type: 'bar',
          data: expenses.map((value, index) => ({
            value,
            itemStyle: {
              color: theme.brand,
              opacity: props.selectedDate && props.points[index]?.date !== props.selectedDate ? 0.35 : 0.9,
              borderRadius: [3, 3, 0, 0]
            }
          })),
          barMaxWidth: props.period === 'year' ? 10 : 5
        },
        {
          name: '结余',
          type: 'line',
          data: surpluses,
          smooth: 0.25,
          symbol: 'circle',
          symbolSize: 5,
          lineStyle: { color: theme.property, width: 2 },
          itemStyle: { color: theme.property }
        }
      ]
    }
  }

  const direction = props.mode
  const color = direction === 'income' ? theme.loss : theme.brand
  const values = props.points.map((point) => point[direction])
  const maxValue = values.length ? Math.max(...values) : 0
  const maxIndex = maxValue > 0 ? values.indexOf(maxValue) : -1
  const averageValue = props.average && props.average > 0 ? props.average : null

  return {
    animation: animate,
    animationDuration: 280,
    grid: { left: 46, right: 12, top: 20, bottom: 24 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
      axisPointer: { type: 'shadow', shadowStyle: { color: theme.pointerShadow } },
      formatter: (params: unknown) => {
        const first = Array.isArray(params) ? params[0] as { dataIndex?: number } | undefined : undefined
        const point = first?.dataIndex === undefined ? undefined : props.points[first.dataIndex]
        if (!point) return ''
        const lines = [
          `<strong>${point.label}</strong>`,
          `${direction === 'income' ? '收入' : '支出'} ¥${formatMoney(point[direction])}`
        ]
        if (props.period === 'month' && point.category && point[direction] > 0) {
          lines.push(`主要：${point.category}`)
        }
        return lines.join('<br/>')
      }
    },
    xAxis: {
      type: 'category',
      data: labels,
      axisTick: { show: false },
      axisLine: { lineStyle: { color: theme.axis } },
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(11),
        interval: xInterval
      }
    },
    yAxis: {
      type: 'value',
      min: 0,
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(11),
        formatter: formatAxisMoney
      },
      splitLine: { lineStyle: { color: theme.grid } }
    },
    series: [
      {
        name: direction === 'income' ? '收入' : '支出',
        type: 'bar',
        data: values.map((value, index) => {
          const isSelected = Boolean(props.selectedDate) && props.points[index]?.date === props.selectedDate
          const hasSelection = Boolean(props.selectedDate)
          return {
            value,
            itemStyle: {
              color,
              opacity: hasSelection && !isSelected ? 0.32 : 1,
              borderRadius: [4, 4, 0, 0]
            }
          }
        }),
        barMaxWidth: props.period === 'year' ? 18 : 9,
        barCategoryGap: '34%',
        markPoint: maxIndex >= 0
          ? {
              symbol: 'circle',
              symbolSize: 7,
              label: { show: false },
              itemStyle: { color, borderColor: theme.tooltipBg, borderWidth: 2 },
              data: [{ name: '峰值', coord: [maxIndex, maxValue] }]
            }
          : undefined,
        markLine: averageValue
          ? {
              silent: true,
              symbol: ['none', 'none'],
              lineStyle: { type: 'dashed', color: theme.pointer, width: 1.2 },
              label: {
                position: 'insideEndTop',
                color: theme.text,
                fontSize: chartFontSize(10),
                formatter: `${props.period === 'year' ? '月均' : '日均'} ¥${formatAxisMoney(averageValue)}`
              },
              data: [{ yAxis: averageValue }]
            }
          : undefined
      }
    ]
  }
}

function handleInit(chart: ECharts): void {
  chart.on('click', (params: { dataIndex?: number }) => {
    const index = params.dataIndex
    if (index === undefined) return
    const point = props.points[index]
    if (point) {
      emit('selectPoint', point)
    }
  })
}

useEChart(container, buildOption, {
  watchSources: () => [props.points, props.points.length, props.mode, props.period, props.average, props.selectedDate],
  onInit: handleInit
})
</script>

<template>
  <div
    ref="container"
    class="stats-chart"
    role="img"
    :aria-label="`${mode === 'income' ? '收入' : mode === 'balance' ? '收支对比' : '支出'}趋势图，合计 ${formatMoney(total)} 元`"
  />
</template>

<style scoped>
.stats-chart {
  width: 100%;
  height: 204px;
}
</style>
