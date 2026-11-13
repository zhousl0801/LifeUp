import { flushPromises, mount } from '@vue/test-utils'
import { expect, test, vi } from 'vitest'
import type { CapitalProjectionPoint } from '../types/capital'
import CapitalProjectionChart from './CapitalProjectionChart.vue'

const chart = vi.hoisted(() => ({
  setOption: vi.fn(),
  resize: vi.fn(),
  dispose: vi.fn()
}))
const runtime = vi.hoisted(() => ({ load: vi.fn() }))

vi.mock('../utils/chartRuntime', () => ({ loadChartRuntime: runtime.load }))

interface ChartOption {
  series: { name: string; type: string; markLine?: unknown }[]
  xAxis: { data: string[] }
}

function monthAt(index: number): string {
  const date = new Date(Date.UTC(2026, 11 + index, 1))
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`
}

const points: CapitalProjectionPoint[] = Array.from({ length: 14 }, (_, index) => ({
  month: monthAt(index),
  capital: 384_050 + index * 1_000,
  netAccumulation: 6_800,
  return: 1_250
}))

test('renders the capital option and switches to the monthly bars', async () => {
  runtime.load.mockResolvedValue({ init: () => chart })
  const view = mount(CapitalProjectionChart, {
    props: {
      points,
      targetAmount: 1_000_000,
      view: 'capital',
      amountsMasked: false,
      currentTotal: 376_000,
      currentMonth: '2026-11'
    }
  })
  await flushPromises()

  const capital = chart.setOption.mock.calls[0]![0] as ChartOption

  expect(capital.series.map((item) => item.name)).toEqual(['净积累', '投资收益', '总资本'])
  expect(capital.series[2]!.markLine).toBeDefined()
  expect(capital.xAxis.data).toHaveLength(15)

  const monthlyButton = view.findAll('button').find((button) => button.text() === '每月积累')!
  await monthlyButton.trigger('click')
  expect(view.emitted('update:view')?.[0]).toEqual(['monthly'])

  await view.setProps({ view: 'monthly' })
  await flushPromises()

  const monthly = chart.setOption.mock.calls.at(-1)![0] as ChartOption

  expect(monthly.series[0]!.type).toBe('bar')
  expect(monthly.xAxis.data).toHaveLength(12)
  expect(view.find('.chart-summary').text()).toContain('首年月均')

  view.unmount()
  expect(chart.dispose).toHaveBeenCalledOnce()
})
