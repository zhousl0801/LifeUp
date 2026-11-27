import type { EChartsOption } from 'echarts'
import type { CapitalProjectionPoint, FireOverview } from '../types/capital'
import { formatMonthLabel } from './capital'
import { chartFontSize, chartTheme } from './chartTheme'
import { MaskedMoney, formatMoney } from './format'

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

  const amount = (value: number) => input.amountsMasked ? MaskedMoney : `¥${formatMoney(value)}`
  const targetLabel = input.targetAmount !== null
    ? input.amountsMasked ? '••••' : formatWan(input.targetAmount)
    : ''
  const max = Math.max(input.targetAmount ?? 0, totals[totals.length - 1] ?? 0, 1) * 1.05

  return {
    animation: false,
    grid: { left: input.amountsMasked ? 28 : 8, right: 28, top: 26, bottom: 26, containLabel: !input.amountsMasked },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
      axisPointer: { type: 'line', lineStyle: { color: theme.pointer, type: 'dashed' } },
      formatter: (params: { dataIndex?: number }[]) => {
        const index = params[0]?.dataIndex

        if (index === undefined) {
          return ''
        }

        return `${months[index]}<br/>总资本 ${amount(totals[index] ?? 0)}<br/>净积累 ${amount(net[index] ?? 0)}<br/>投资收益 ${amount(returns[index] ?? 0)}`
      }
    },
    xAxis: projectionMonthAxis(months),
    yAxis: {
      type: 'value',
      show: !input.amountsMasked,
      max,
      splitNumber: 4,
      axisLabel: { color: theme.text, fontSize: chartFontSize(10), formatter: formatWan },
      splitLine: { lineStyle: { color: theme.axis } }
    },
    series: [
      {
        name: '净积累',
        type: 'line',
        stack: 'capital',
        data: net,
        symbol: 'none',
        lineStyle: { width: 0 },
        areaStyle: { color: theme.areaStrong },
        itemStyle: { color: theme.brand }
      },
      {
        name: '投资收益',
        type: 'line',
        stack: 'capital',
        data: returns,
        symbol: 'none',
        lineStyle: { width: 0 },
        areaStyle: { color: theme.propertyArea },
        itemStyle: { color: theme.property }
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
                position: 'insideEndTop',
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

function projectionMonthAxis(months: string[]) {
  const theme = chartTheme()
  return {
    type: 'category' as const,
    data: months,
    boundaryGap: false,
    axisLine: { lineStyle: { color: theme.axis } },
    axisTick: { show: false },
    axisLabel: {
      color: theme.text,
      fontSize: chartFontSize(),
      hideOverlap: true,
      interval: (index: number, value: string) => index === 0
        || index === months.length - 1
        || (value.endsWith('-01') && Number(value.slice(0, 4)) % 2 === 0),
      formatter: (value: string) => value
    }
  }
}

export function buildCashflowChartOption(
  input: Pick<CapitalChartInput, 'points' | 'currentMonth' | 'amountsMasked'>,
  fire: FireOverview
): EChartsOption {
  const theme = chartTheme()
  const months = [input.currentMonth, ...input.points.map((point) => point.month)]
  const passive = [fire.currentPassiveAnnualCashflow, ...input.points.map((point) => point.passiveAnnualCashflow ?? null)]
  const expense = [fire[fire.activeMode].annualExpense, ...input.points.map((point) => point.expectedAnnualExpense ?? null)]
  const crossover = passive.findIndex((value, index) => {
    const expected = expense[index]
    return value != null && expected != null && expected > 0 && value >= expected
  })
  const amount = (value: number) => input.amountsMasked ? '••••' : `¥${formatMoney(value)}`
  const frequency = (value: number | null | undefined) => value == null
    ? '—'
    : `${amount(value)}（${amount(value / 12)}/月）`

  return {
    animation: false,
    grid: { left: 10, right: 26, top: 30, bottom: 26, containLabel: !input.amountsMasked },
    xAxis: projectionMonthAxis(months),
    yAxis: {
      type: 'value',
      show: !input.amountsMasked,
      splitNumber: 4,
      axisLabel: { color: theme.text, fontSize: chartFontSize(), formatter: formatWan },
      splitLine: { lineStyle: { color: theme.axis } }
    },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      className: 'cashflow-tooltip',
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize() },
      axisPointer: { type: 'line', lineStyle: { color: theme.pointer, type: 'dashed' } },
      formatter: (params) => {
        const index = (Array.isArray(params) ? params[0] : params)?.dataIndex
        const month = index === undefined ? undefined : months[index]
        if (index === undefined || !month) return ''

        const annualCashflow = passive[index]
        const annualExpense = expense[index]
        const freedom = annualCashflow != null && annualExpense != null && annualExpense > 0
          ? `${(annualCashflow / annualExpense * 100).toFixed(1)}%`
          : '—'

        return `${formatMonthLabel(month)}<br/>被动年现金流 ${frequency(annualCashflow)}<br/>预期年支出 ${frequency(annualExpense)}<br/>自由度 ${freedom}`
      }
    },
    series: [
      {
        name: '被动年现金流',
        type: 'line',
        smooth: true,
        symbol: 'none',
        data: passive,
        lineStyle: { color: theme.mint, width: 2.4 },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [{ offset: 0, color: theme.mintArea }, { offset: 1, color: 'transparent' }]
          }
        },
        markPoint: crossover < 0 ? undefined : {
          silent: true,
          symbol: 'circle',
          symbolSize: 10,
          itemStyle: { color: theme.mint, shadowColor: theme.mint, shadowBlur: 12 },
          label: {
            show: true, formatter: 'FIRE 自由点', position: 'top',
            align: crossover === 0 ? 'left' : crossover === months.length - 1 ? 'right' : 'center',
            color: theme.text, fontSize: chartFontSize()
          },
          data: [{ name: 'FIRE 自由点', coord: [months[crossover]!, passive[crossover]!] }]
        }
      },
      {
        name: '预期年支出',
        type: 'line',
        symbol: 'none',
        data: expense,
        lineStyle: { color: theme.amber, width: 2, type: 'dashed' }
      }
    ]
  }
}

export function buildMonthlyChartOption(input: CapitalChartInput) {
  const theme = chartTheme()
  const points = input.points.slice(0, 12)

  return {
    animation: false,
    grid: { left: 8, right: 8, top: 12, bottom: 26 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: theme.tooltipBg,
      borderWidth: 0,
      textStyle: { color: theme.tooltipText, fontSize: chartFontSize(12) },
      axisPointer: { type: 'shadow' },
      formatter: (params: { dataIndex?: number }[]) => {
        const index = params[0]?.dataIndex
        const point = index === undefined ? undefined : points[index]

        if (!point) {
          return ''
        }

        const amount = (value: number) => input.amountsMasked
          ? MaskedMoney
          : `¥${formatMoney(value)}`

        return `${Number(point.month.slice(5))}月<br/>净积累 ${amount(point.netAccumulation)}<br/>投资收益 ${amount(point.return)}`
      }
    },
    xAxis: {
      type: 'category',
      data: points.map((point) => `${Number(point.month.slice(5))}月`),
      axisLine: { lineStyle: { color: theme.axis } },
      axisTick: { show: false },
      axisLabel: { color: theme.text, fontSize: chartFontSize(10), interval: 2, hideOverlap: true }
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
        itemStyle: { color: theme.property },
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
