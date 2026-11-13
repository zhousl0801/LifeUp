import { expect, test } from 'vitest'
import { buildAssetTrend, buildTrendOption, trendAxisLabel, trendTooltip } from './trend'
import type { AssetTrend, AssetTrendPoint } from '../types/trend'

function point(overrides: Partial<AssetTrendPoint>): AssetTrendPoint {
  return {
    date: '2026-10-01',
    totalAssets: null,
    knownTotalAssets: 0,
    cash: null,
    provident: null,
    fundValue: null,
    fundPending: 0,
    fundPendingArrival: 0,
    goldValue: null,
    goldPendingArrival: 0,
    isComplete: false,
    hasCash: false,
    hasProvident: false,
    hasFunds: false,
    hasGold: false,
    ...overrides
  }
}

test('uses the complete daily total and the recorded subtotal on incomplete dates', () => {
  const trend: AssetTrend = {
    range: 'month',
    from: '2026-10-01',
    to: '2026-10-04',
    notes: [],
    points: [
      point({ date: '2026-10-01' }),
      point({ date: '2026-10-02', cash: 1000, knownTotalAssets: 1000, hasCash: true }),
      point({ date: '2026-10-03', cash: 900, goldValue: 500, knownTotalAssets: 1400, hasCash: true, hasGold: true }),
      point({ date: '2026-10-04', cash: 850, goldValue: 600, totalAssets: 1450, knownTotalAssets: 1450, isComplete: true, hasCash: true, hasGold: true })
    ]
  }

  const series = buildAssetTrend(trend)
  const option = buildTrendOption(trend, series)
  expect(option.series[0]!.data).toEqual([null, 1000, 1400, 1450])
  expect(series.latest?.value).toBe(1450)
  expect(option.series[0]!.markPoint.data).toEqual([
    expect.objectContaining({ coord: ['2026-10-03', 1400], symbol: 'circle', symbolSize: 12 })
  ])
  expect(trendTooltip(series.points[1]!, true)).toContain('¥1••••')
  expect(option.series[0]!.markPoint.label.show).toBe(false)
  expect(trendTooltip(series.points[2]!)).toContain('已记录资产 ¥1,400.00')
  expect(trendTooltip(series.points[2]!)).not.toContain('新增')
  expect(trendTooltip(series.points[3]!)).toContain('总资产 ¥1,450.00')
  expect(trendAxisLabel('2026-10-02', 'month')).toBe('10-02')
  expect(trendAxisLabel('2026-10-02', 'all')).toBe('2026-10')
})
