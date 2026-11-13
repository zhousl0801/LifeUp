import { formatMoney, formatMaskedMoney } from './format'
import { chartFontSize, chartTheme, type ChartThemeVariant } from './chartTheme'
import type { AssetTrend, AssetTrendPoint, TrendRange } from '../types/trend'

interface DailyAssetPoint {
  date: string
  value: number | null
  source: AssetTrendPoint
}

const assetFlags = ['hasCash', 'hasProvident', 'hasFunds', 'hasGold', 'hasStocks'] as const

export function buildAssetTrend(trend: AssetTrend) {
  const seenAssets = new Set<typeof assetFlags[number]>()
  const additions: [string, number][] = []
  const points = trend.points.map((point) => {
    const recorded = assetFlags.filter(flag => point[flag])
    const value = point.totalAssets ?? (recorded.length > 0 ? point.knownTotalAssets : null)
    const hasAddition = seenAssets.size > 0 && recorded.some(flag => !seenAssets.has(flag))
    if (hasAddition && value !== null) additions.push([point.date, value])
    recorded.forEach(flag => seenAssets.add(flag))
    return { date: point.date, value, source: point }
  })

  return {
    points,
    additions,
    latest: [...points].reverse().find(point => point.value !== null) ?? points.at(-1) ?? null
  }
}

export function trendAmountLabel(point: DailyAssetPoint | null | undefined, amountsMasked = false): string {
  if (point?.value == null) return '依据不足'

  if (amountsMasked) return formatMaskedMoney(point.value)
  return `¥${formatMoney(point.value)}`
}

export function trendAxisLabel(date: string, range: TrendRange): string {
  return range === 'quarter' || range === 'all' ? date.slice(0, 7) : date.slice(5)
}

export function trendTooltip(point: DailyAssetPoint, amountsMasked = false): string {
  const label = point.source.totalAssets === null ? '已记录资产' : '总资产'
  return `${point.date}<br/>${label} ${trendAmountLabel(point, amountsMasked)}`
}

export function buildTrendOption(
  trend: AssetTrend,
  series: ReturnType<typeof buildAssetTrend>,
  variant: ChartThemeVariant = 'default',
  amountsMasked = false
) {
  const theme = chartTheme(variant)
  const dates = series.points.map((point) => point.date)
  const values = series.points.map((point) => point.value)
  const isHero = variant === 'hero'

  return {
    animation: false,
    grid: isHero
      ? { left: 7, right: 7, top: 10, bottom: 8 }
      : { left: 10, right: 46, top: 28, bottom: 24 },
    tooltip: {
      trigger: 'axis',
      confine: true,
      backgroundColor: isHero ? 'rgba(46, 43, 70, 0.96)' : undefined,
      borderWidth: 0,
      textStyle: isHero ? { color: '#F7F5FF', fontSize: chartFontSize(12) } : undefined,
      axisPointer: { type: 'line', lineStyle: { color: theme.pointer, type: 'dashed' } },
      formatter: (params: { dataIndex?: number }[]) => {
        const index = params[0]?.dataIndex

        if (index === undefined || !series.points[index]) {
          return ''
        }

        return trendTooltip(series.points[index]!, amountsMasked)
      }
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: dates,
      axisLabel: {
        show: !isHero,
        color: theme.text,
        fontSize: chartFontSize(12),
        formatter: (value: string) => trendAxisLabel(value, trend.range)
      },
      axisLine: { show: !isHero, lineStyle: { color: theme.axis } },
      axisTick: { show: false }
    },
    yAxis: {
      type: 'value',
      scale: true,
      axisLabel: { show: false },
      axisLine: { show: false },
      axisTick: { show: false },
      splitLine: { show: false }
    },
    series: [
      {
        name: '资产合计',
        type: 'line',
        data: values,
        showSymbol: false,
        connectNulls: false,
        smooth: false,
        lineStyle: { width: 2, color: theme.brand },
        itemStyle: { color: theme.brand },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: theme.areaStrong },
              { offset: 1, color: theme.areaSoft }
            ]
          }
        },
        markPoint: {
          silent: true,
          label: { show: false },
          data: series.additions.map(coord => ({
            coord,
            symbol: 'circle',
            symbolSize: 12,
            itemStyle: { color: theme.marker }
          }))
        }
      }
    ]
  }
}
