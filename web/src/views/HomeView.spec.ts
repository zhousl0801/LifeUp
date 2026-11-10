import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { beforeEach, expect, test, vi } from 'vitest'
import HomeView from './HomeView.vue'
import { todayInBusinessTimeZone } from '../utils/format'
import { useMessages } from '../composables/useMessage'
import type { MonthChange } from '../types/ledger'
import type { AssetTrend } from '../types/trend'

const mocks = vi.hoisted(() => ({
  ledger: vi.fn(),
  trend: vi.fn(),
  dayChange: vi.fn(),
  change: vi.fn(),
  plans: vi.fn(),
  quotes: vi.fn()
}))

vi.mock('../api/ledger', () => ({
  fetchLedger: mocks.ledger,
  fetchAssetTrend: mocks.trend,
  fetchDayChange: mocks.dayChange,
  fetchMonthChange: mocks.change,
  updateAssetCoverage: vi.fn(),
  initializeLedger: vi.fn()
}))
vi.mock('../api/plans', () => ({ fetchPlanOverview: mocks.plans }))
vi.mock('../api/quotes', () => ({ refreshQuotes: mocks.quotes }))
vi.mock('vue-router', () => ({
  RouterLink: { props: ['to'], template: '<a><slot /></a>' },
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}))
vi.mock('../components/AssetTrendChart.vue', () => ({ default: { template: '<div class="chart-stub" />' } }))

const today = todayInBusinessTimeZone()

function shift(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00Z`)
  value.setUTCDate(value.getUTCDate() + days)
  return value.toISOString().slice(0, 10)
}

function overview(startDate: string, overrides: Record<string, unknown> = {}) {
  return {
    id: 'ledger',
    currency: 'CNY',
    timeZone: 'Asia/Shanghai',
    startDate,
    asOfDate: today,
    isComplete: false,
    totalAssets: 355851.75,
    cash: { isConfigured: true, amount: 244685, note: '统一余额', notHeld: false },
    fund: { isConfigured: false, amount: null, note: '尚未录入', notHeld: false },
    providentFund: { isConfigured: true, amount: 100345, note: '本月暂无缴存', notHeld: false },
    gold: { isConfigured: true, amount: 10821.75, note: '持有 12.4948 克', notHeld: false },
    stock: { isConfigured: false, amount: null, note: '尚未录入', notHeld: false },
    month: { income: 0, expense: 0, surplus: 0, hasRecords: false },
    fundNotHeld: false,
    providentFundNotHeld: false,
    goldNotHeld: false,
    stockNotHeld: false,
    coverageComplete: false,
    ...overrides
  }
}

function point(date: string, overrides: Record<string, unknown>) {
  return {
    date,
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

function trend(startDate: string): AssetTrend {
  return {
    range: 'month',
    from: startDate,
    to: today,
    notes: [],
    points: [
      point(startDate, { cash: 244685, hasCash: true }),
      point(today, { cash: 244685, provident: 100345, goldValue: 10821.75, hasCash: true, hasProvident: true, hasGold: true })
    ]
  }
}

const monthChange: MonthChange = {
  from: today,
  to: today,
  totalChange: null,
  livingSurplus: 0,
  providentDeposit: 0,
  investmentProfit: null,
  reconciliationDifference: 0,
  isComplete: false,
  notes: [],
  assetChanges: [
    { assetType: 'cash', change: 0, isNew: false },
    { assetType: 'providentFund', change: null, isNew: true },
    { assetType: 'fund', change: null, isNew: false },
    { assetType: 'gold', change: null, isNew: true }
  ],
  comparableChange: null,
  comparableBase: null,
  addedRecords: 111166.75,
  marketValueChange: null,
  netDeposit: null,
  hasComparableData: false
}

const { messages, clear } = useMessages()

beforeEach(() => {
  clear()
  vi.clearAllMocks()
  mocks.dayChange.mockResolvedValue(structuredClone(monthChange))
  mocks.change.mockResolvedValue(structuredClone(monthChange))
})

test.each([
  { income: 300, expense: 200, incomeWidth: '60%', expenseWidth: '40%', dominant: undefined },
  { income: 400, expense: 100, incomeWidth: '80%', expenseWidth: '20%', dominant: 'income' },
  { income: 100, expense: 400, incomeWidth: '20%', expenseWidth: '80%', dominant: 'expense' },
  { income: 500, expense: 0, incomeWidth: '100%', expenseWidth: '0%', dominant: 'income' },
  { income: 0, expense: 500, incomeWidth: '0%', expenseWidth: '100%', dominant: 'expense' },
  { income: 0, expense: 0, incomeWidth: '0%', expenseWidth: '0%', dominant: undefined }
])('shows a shared monthly scale for income $income and expense $expense', async ({
  income, expense, incomeWidth, expenseWidth, dominant
}) => {
  const start = shift(today, -1)
  mocks.ledger.mockResolvedValue(overview(start, {
    month: { income, expense, surplus: income - expense, hasRecords: income + expense > 0 }
  }))
  mocks.trend.mockResolvedValue(trend(start))

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  const card = view.get('.month-flow')
  const segments = card.findAll<HTMLElement>('.month-flow-segment')
  expect(segments.map((segment) => segment.element.style.width)).toEqual([incomeWidth, expenseWidth])
  expect(card.get('.month-flow-track').attributes('data-dominant')).toBe(dominant)
  expect(card.text()).toContain(`¥${income.toFixed(2)}`)
  expect(card.text()).toContain(`¥${expense.toFixed(2)}`)
  expect(card.get('.month-surplus').text()).toContain(Math.abs(income - expense).toFixed(2))
  expect(card.text()).not.toContain('查看账本')
  view.unmount()
})

test('shows recorded assets, no comparable month change and the seven-day trend gate', async () => {
  const start = shift(today, -1)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  expect(view.find('.hero-label').text()).toContain('已记录资产')
  expect(view.find('.hero-change').text()).toContain('暂无可比数据')
  expect(view.find('.added-note').exists()).toBe(false)
  expect(view.find('.hero-meta').exists()).toBe(false)
  expect(view.find('.trend-panel h2').text()).toBe('资产趋势')
  expect(view.find('.trend-wait').text()).toContain('记录满 7 天后显示趋势')
  expect(view.find('.trend-wait').text()).toContain('已记录 1/7 天')
  const distribution = view.get('.distribution')
  expect(distribution.text()).not.toContain('管理')
  expect(view.text()).not.toContain('自动记账')
  expect(mocks.plans).not.toHaveBeenCalled()
  expect(distribution.get('article').text()).toContain('68.8%')
  await distribution.get('button[aria-label="基金"]').trigger('click')
  expect(distribution.get('article').text()).toContain('暂未录入')
  expect(view.find('.chart-stub').exists()).toBe(false)
  view.unmount()
})

test('draws the scoped trend once seven days are recorded', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  expect(mocks.trend).toHaveBeenCalledWith('week')
  expect(view.find('.range-button.active').text()).toBe('7日')
  expect(view.find('.chart-stub').exists()).toBe(true)
  expect(view.find('.chart-readout').text()).toContain('244,685.00')
  expect(view.find('.chart-note').exists()).toBe(false)
  expect(view.find('.trend-wait').exists()).toBe(false)
  view.unmount()
})

test('defaults to today change and switches to month change', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...structuredClone(monthChange),
    hasComparableData: true,
    comparableChange: 1000,
    comparableBase: 20000,
    totalChange: 1000,
    marketValueChange: 600,
    netDeposit: 400
  })
  mocks.change.mockResolvedValue({
    ...structuredClone(monthChange),
    hasComparableData: true,
    comparableChange: 2000,
    comparableBase: 40000,
    totalChange: 2000,
    marketValueChange: 1200,
    netDeposit: 800
  })

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  expect(view.find('.change-scope button.active').text()).toBe('当日')
  expect(view.find('.hero-change').text()).toContain('+1,000.00')
  expect(view.find('.hero-change').text()).toContain('+5.00%')

  await view.findAll('.change-scope button').find((button) => button.text() === '本月')!.trigger('click')

  expect(view.find('.change-scope button.active').text()).toBe('本月')
  expect(view.find('.hero-change').text()).toContain('+2,000.00')
  expect(view.find('.hero-change').text()).toContain('+5.00%')
  view.unmount()
})

test('syncs market quotes from the home header and reloads the overview', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.quotes.mockResolvedValue({
    updated: 3,
    fundSucceeded: 2,
    fundFailed: 0,
    goldSucceeded: 1,
    goldFailed: 0,
    stockSucceeded: 0,
    stockFailed: 0
  })

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  await view.find('.sync-button').trigger('click')
  await flushPromises()

  expect(mocks.quotes).toHaveBeenCalledWith('all')
  expect(messages.value.at(-1)?.text ?? '').toContain('更新 3 项')
  expect(mocks.ledger).toHaveBeenCalledTimes(2)
  view.unmount()
})

test('negative comparable changes show a downward arrow', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({ ...monthChange, hasComparableData: true, comparableChange: -100, comparableBase: 1000 })
  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()
  expect(view.find('.change-arrow').text()).toBe('▼')
  view.unmount()
})
