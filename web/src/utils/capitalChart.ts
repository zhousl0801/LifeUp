import type { CapitalProjectionPoint } from '../types/capital'
import { chartFontSize, chartTheme } from './chartTheme'
import { formatMaskedMoney, formatMoney } from './format'

const ReturnColor = '#25C6DA'

export interface CapitalChartInput {
  points: CapitalProjectionPoint[]
  targetAmount: number | null
  currentTotal: number
  currentMonth: string
  amountsMasked: boolean
}

export function buildCapitalChartOption(input: CapitalChartInput) {
  const theme = chartTheme()
  const months = [input.currentMonth, ...input.points.map((point) => point.month)]
  const net: number[] = []
  const returns: number[] = []
  const totals: number[] = []
  let netCumulative = input.currentTotal
  let returnCumulative = 0

  net.push(round(netCumulative))
  returns.push(0)
  totals.push(input.currentTotal)

  for (const point of input.points) {
    netCumulative += point.netAccumulation
    returnCumulative += point.return
    net.push(round(netCumulative))
    returns.push(round(returnCumulative))
    totals.push(point.capital)
  }

  const amount = (value: number) => input.amountsMasked ? formatMaskedMoney(value) : `¥${formatMoney(value)}`
  const targetLabel = input.targetAmount !== null ? formatWan(input.targetAmount) : ''
  const max = Math.max(input.targetAmount ?? 0, totals[totals.length - 1] ?? 0, 1) * 1.05

  return {
    animation: false,
    grid: { left: 8, right: 8, top: 26, bottom: 22 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      borderWidth: 0,
      axisPointer: { type: 'line', lineStyle: { color: theme.pointer, type: 'dashed' } },
      formatter: (params: { dataIndex?: number }[]) => {
        const index = params[0]?.dataIndex

        if (index === undefined) {
          return ''
        }

        return `${months[index]}<br/>总资本 ${amount(totals[index] ?? 0)}<br/>净积累 ${amount(net[index] ?? 0)}<br/>投资收益 ${amount(returns[index] ?? 0)}`
      }
    },
    xAxis: {
      type: 'category',
      data: months,
      boundaryGap: false,
      axisLine: { lineStyle: { color: theme.axis } },
      axisTick: { show: false },
      axisLabel: {
        color: theme.text,
        fontSize: chartFontSize(9),
        interval: (index: number, value: string) => index === 0
          || index === months.length - 1
          || (value.endsWith('-01') && Number(value.slice(0, 4)) % 2 === 0),
        formatter: (value: string) => value.slice(0, 4)
      }
    },
    yAxis: {
      type: 'value',
      show: false,
      max
    },
    series: [
      {
        name: '净积累',
        type: 'line',
        stack: 'capital',
        data: net,
        symbol: 'none',
        lineStyle: { width: 0 },
        areaStyle: { color: 'rgba(91, 91, 215, 0.14)' },
        itemStyle: { color: theme.brand }
      },
      {
        name: '投资收益',
        type: 'line',
        stack: 'capital',
        data: returns,
        symbol: 'none',
        lineStyle: { width: 0 },
        areaStyle: { color: 'rgba(37, 198, 218, 0.42)' },
        itemStyle: { color: ReturnColor }
      },
      {
        name: '总资本',
        type: 'line',
        data: totals,
        symbol: 'none',
        smooth: true,
        lineStyle: { width: 2.2, color: theme.brand },
        itemStyle: { color: theme.brand },
        markLine: input.targetAmount !== null
          ? {
              silent: true,
              symbol: 'none',
              lineStyle: { color: theme.pointer, type: 'dashed' },
              label: {
                formatter: `目标 ${targetLabel}`,
                color: theme.text,
                fontSize: chartFontSize(10)
              },
              data: [{ yAxis: input.targetAmount }]
            }
          : undefined
      }
    ]
  }
}

export function buildMonthlyChartOption(input: CapitalChartInput) {
  const theme = chartTheme()
  const points = input.points.slice(0, 12)

  return {
    animation: false,
    grid: { left: 8, right: 8, top: 12, bottom: 22 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      borderWidth: 0,
      axisPointer: { type: 'shadow' }
    },
    xAxis: {
      type: 'category',
      data: points.map((point) => `${Number(point.month.slice(5))}月`),
      axisLine: { lineStyle: { color: theme.axis } },
      axisTick: { show: false },
      axisLabel: { color: theme.text, fontSize: chartFontSize(9), interval: 2 }
    },
    yAxis: {
      type: 'value',
      show: false
    },
    series: [
      {
        name: '净积累',
        type: 'bar',
        stack: 'monthly',
        data: points.map((point) => point.netAccumulation),
        itemStyle: { color: theme.brand },
        barMaxWidth: 18
      },
      {
        name: '投资收益',
        type: 'bar',
        stack: 'monthly',
        data: points.map((point) => point.return),
        itemStyle: { color: ReturnColor },
        barMaxWidth: 18
      }
    ]
  }
}

export function monthlySummary(points: CapitalProjectionPoint[]): { average: number; investment: number } {
  const firstYear = points.slice(0, 12)
  const count = Math.max(1, firstYear.length)
  const average = firstYear.reduce((sum, point) => sum + point.netAccumulation + point.return, 0) / count
  const investment = firstYear.reduce((sum, point) => sum + point.return, 0) / count

  return { average: round(average), investment: round(investment) }
}

function formatWan(value: number): string {
  if (value >= 10_000) {
    return `${new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 }).format(value / 10_000)} 万`
  }

  return `${new Intl.NumberFormat('en-US').format(value)} 元`
}

function round(value: number): number {
  return Math.round(value * 100) / 100
}
