import { flushPromises, mount } from '@vue/test-utils'
import { expect, test, vi } from 'vitest'
import AssetTrendChart from './AssetTrendChart.vue'
import type { AssetTrend } from '../types/trend'

const chart = vi.hoisted(() => ({
  on: vi.fn(), setOption: vi.fn(), resize: vi.fn(), dispose: vi.fn()
}))
const runtime = vi.hoisted(() => ({
  load: vi.fn()
}))
vi.mock('../utils/chartRuntime', () => ({ loadChartRuntime: runtime.load }))

test('selecting a trend date reports the current point without changing its financial values', async () => {
  runtime.load.mockResolvedValue({ init: () => chart })
  const point = {
    date: '2026-10-01', totalAssets: null, knownTotalAssets: 1200, cash: 1200,
    provident: null, fundValue: 0, fundPending: 0, fundPendingArrival: 0,
    goldValue: 0, goldPendingArrival: 0, isComplete: false,
    hasCash: true, hasProvident: false, hasFunds: false, hasGold: false
  }
  const trend: AssetTrend = { range: 'month', from: point.date, to: point.date, points: [point], notes: [] }
  const view = mount(AssetTrendChart, { props: { trend } })
  await flushPromises()
  const select = chart.on.mock.calls[0]![1] as (event: unknown) => void

  select({ axesInfo: [{ axisDim: 'x', axisIndex: 0, value: 0 }] })
  expect(view.emitted('select')?.[0]).toEqual([point])

  const nextPoint = { ...point, date: '2026-10-02', totalAssets: 1500, knownTotalAssets: 1500, isComplete: true }
  await view.setProps({ trend: { ...trend, points: [nextPoint] } })
  select({ axesInfo: [{ axisDim: 'x', axisIndex: 0, value: 0 }] })
  expect(view.emitted('select')?.[1]).toEqual([nextPoint])
  select({ axesInfo: [] })
  expect(view.emitted('select')).toHaveLength(2)
  view.unmount()
  expect(chart.dispose).toHaveBeenCalledOnce()
})

test('renders baseline dots while loading and transitions to the line when data arrives', async () => {
  chart.on.mockClear()
  chart.setOption.mockClear()
  chart.dispose.mockClear()
  runtime.load.mockResolvedValue({ init: () => chart })

  const view = mount(AssetTrendChart, {
    props: { trend: null, placeholderRange: 'week', placeholderEndDate: '2026-10-07', variant: 'hero' }
  })
  await flushPromises()

  expect(chart.setOption).toHaveBeenCalledWith(
    expect.objectContaining({
      yAxis: expect.objectContaining({ min: 0, max: 1 }),
      series: [expect.objectContaining({ data: [0, 0, 0, 0, 0, 0, 0], showSymbol: true })]
    }),
    { notMerge: false, replaceMerge: ['yAxis'] }
  )

  const select = chart.on.mock.calls[0]![1] as (event: unknown) => void
  select({ axesInfo: [{ axisDim: 'x', axisIndex: 0, value: 0 }] })
  expect(view.emitted('select')).toBeUndefined()

  const point = {
    date: '2026-10-07', totalAssets: 2000, knownTotalAssets: 2000, cash: 2000,
    provident: null, fundValue: 0, fundPending: 0, fundPendingArrival: 0,
    goldValue: 0, goldPendingArrival: 0, isComplete: true,
    hasCash: true, hasProvident: false, hasFunds: false, hasGold: false
  }
  await view.setProps({ trend: { range: 'week', from: point.date, to: point.date, points: [point], notes: [] } })

  expect(chart.setOption).toHaveBeenLastCalledWith(
    expect.objectContaining({
      yAxis: expect.objectContaining({ scale: true }),
      series: [expect.objectContaining({ data: [2000], showSymbol: false })]
    }),
    { notMerge: false, replaceMerge: ['yAxis'] }
  )

  select({ axesInfo: [{ axisDim: 'x', axisIndex: 0, value: 0 }] })
  expect(view.emitted('select')?.[0]).toEqual([point])
  view.unmount()
})
