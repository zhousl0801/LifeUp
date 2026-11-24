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
  funds: vi.fn(),
  trend: vi.fn(),
  dayChange: vi.fn(),
  change: vi.fn(),
  plans: vi.fn(),
  quotes: vi.fn()
}))

vi.mock('../api/funds', () => ({ fetchFundOverview: mocks.funds }))

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
  useRoute: () => ({ name: 'home', query: {} }),
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
      point(startDate, { cash: 244685, knownTotalAssets: 244685, hasCash: true }),
      point(today, { cash: 244685, provident: 100345, goldValue: 10821.75, knownTotalAssets: 355851.75, hasCash: true, hasProvident: true, hasGold: true })
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

  await view.get('.hero-amount button[aria-label="显示资产金额"]').trigger('click')
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
  expect(distribution.get('[aria-label="现金资产"]').text()).not.toContain('68.8%')
  await view.get('button[aria-label="显示资产金额"]').trigger('click')
  expect(distribution.get('[aria-label="现金资产"]').text()).toContain('68.8%')
  expect(distribution.get('[aria-label="基金资产"]').text()).toContain('暂未录入')
  expect(view.find('.chart-stub').exists()).toBe(false)
  view.unmount()
})

test('draws the daily asset totals once seven days are recorded', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start, {
    fund: { isConfigured: true, amount: 278788.02, note: '持有 1 只基金', notHeld: false }
  }))
  mocks.trend.mockResolvedValue(trend(start))

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  expect(mocks.trend).toHaveBeenCalledWith('week')
  expect(view.find('.range-button.active').text()).toBe('7日')
  expect(view.find('.chart-stub').exists()).toBe(true)
  expect(view.get('.chart-readout').text()).toContain('¥••••')
  expect(view.get('.hero-value').text()).toBe('¥••••')
  expect(view.get('.chart-readout').text()).toContain('已记录资产')
  expect(view.find('.distribution-head').exists()).toBe(false)
  const card = view.get('[aria-label="现金资产"]')
  const fundCard = view.get('[aria-label="基金资产"]')
  expect(card.get('.card-balance').text()).toBe('¥••••')
  expect(card.find('.card-visibility').exists()).toBe(false)
  await view.get('.hero-amount button[aria-label="显示资产金额"]').trigger('click')
  expect(view.get('.hero-value').text()).toBe('¥355,851.75')
  expect(view.get('.chart-readout').text()).toContain('¥355,851.75')
  expect(card.get('.card-balance').text()).toBe('¥244,685.00')
  await view.get('.hero-amount button[aria-label="隐藏资产金额"]').trigger('click')
  expect(view.get('.hero-value').text()).toBe('¥••••')
  expect(view.get('.chart-readout').text()).toContain('¥••••')
  expect(card.get('.card-balance').text()).toBe('¥••••')
  expect(fundCard.get('.card-balance').text()).toBe('¥••••')
  await view.get('.hero-amount button[aria-label="显示资产金额"]').trigger('click')
  expect(fundCard.get('.card-balance').text()).toBe('¥278,788.02')
  expect(view.find('.chart-note').exists()).toBe(false)
  expect(view.find('.trend-wait').exists()).toBe(false)
  view.unmount()
})

test('keeps the selected privacy state when returning to the overview', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))

  const pinia = createPinia()
  const mountHome = () => mount(HomeView, { global: { plugins: [pinia] } })
  const first = mountHome()
  await flushPromises()
  await first.get('button[aria-label="显示资产金额"]').trigger('click')
  first.unmount()

  const returned = mountHome()
  await flushPromises()
  expect(returned.get('.hero-value').text()).toBe('¥355,851.75')
  expect(returned.find('button[aria-label="隐藏资产金额"]').exists()).toBe(true)
  returned.unmount()
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
  expect(view.get('.hero-change').text()).toContain('••••')
  expect(view.get('.hero-change').text()).not.toContain('+1,000.00')
  expect(view.get('.hero-change').text()).not.toContain('+5.00%')
  await view.get('.hero-change').trigger('click')
  expect(document.body.querySelector('.breakdown-total b')?.textContent).toBe('••••')
  document.body.querySelector<HTMLButtonElement>('.sheet-close')!.click()
  await flushPromises()
  await view.get('.hero-amount button[aria-label="显示资产金额"]').trigger('click')
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

  let finishReload!: (value: ReturnType<typeof overview>) => void
  mocks.ledger.mockReturnValueOnce(new Promise(resolve => { finishReload = resolve }))
  await view.find('.sync-button').trigger('click')
  await flushPromises()
  expect(messages.value).toHaveLength(0)
  expect(view.get('.sync-button').attributes('disabled')).toBeDefined()

  finishReload(overview(start))
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
  expect(view.find('.change-arrow').exists()).toBe(false)
  await view.get('button[aria-label="显示资产金额"]').trigger('click')
  expect(view.get('.change-arrow').text()).toBe('▼')
  view.unmount()
})

test('explains the daily increases and decreases with separate cash and investment amounts', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...monthChange,
    from: today, to: today, hasComparableData: true, comparableChange: 1100, comparableBase: 25000,
    addedRecords: 2000,
    dayBreakdown: {
      cashIncome: 500, cashExpense: 120, providentDeposit: 1000,
      fund: { profit: 200, previousValue: 1000, currentValue: 1200, cashTransfer: 0, openingValue: 0, adjustment: 0 },
      stock: { profit: -5, previousValue: 0, currentValue: 3000, cashTransfer: -1005, openingValue: 2000, adjustment: 0 },
      gold: { profit: -500, previousValue: 9000, currentValue: 8500, cashTransfer: 0, openingValue: 0, adjustment: 0 },
      cashAdjustment: 25, holdingAdjustment: 0, otherDifference: 0
    }
  })
  const view = mount(HomeView, { attachTo: document.body, global: { plugins: [createPinia()] } })
  await flushPromises()
  try {
    await view.get('.hero-change').trigger('click')
    const sheet = document.body.querySelector('[role="dialog"]')!
    expect(sheet.textContent).toContain('基金盈亏')
    expect(sheet.textContent).not.toMatch(/500\.00|120\.00|1,100\.00|2,000\.00|1,005\.00/)
    document.body.querySelector<HTMLButtonElement>('.sheet-close')!.click()
    await flushPromises()
    await view.get('button[aria-label="显示资产金额"]').trigger('click')
    await view.get('.hero-change').trigger('click')
    const rows = [...document.body.querySelectorAll('.breakdown-row')].map(row => row.textContent?.trim())
    expect(rows).toEqual(expect.arrayContaining([
      '现金收入+500.00', '现金支出−120.00', '公积金缴存+1,000.00',
      '基金盈亏+200.00', '股票盈亏−5.00', '贵金属盈亏−500.00', '现金核对差额+25.00',
      '新增记录（不计入变化）+2,000.00'
    ]))
    expect(document.body.querySelector('.breakdown-total b')?.textContent).toBe('+1,100.00')
    const stock = [...document.body.querySelectorAll('details')].find(item => item.querySelector('summary')?.textContent?.includes('股票盈亏'))
    expect(stock?.textContent).toContain('对应现金变动')
    expect(stock?.textContent).toContain('−1,005.00')
  } finally {
    view.unmount()
  }
})

test('includes earlier NAV profit in the headline and detail total with the NAV date', async () => {
  const start = shift(today, -30)
  const disclosureDate = shift(today, -2)
  mocks.ledger.mockResolvedValue(overview(start, {
    fund: { isConfigured: true, amount: 273349.99, note: null, notHeld: false }
  }))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...monthChange,
    hasComparableData: true,
    comparableChange: -436.64,
    comparableBase: 620000,
    priorFundDisclosure: {
      date: disclosureDate,
      profit: -9198.06,
      covered: 2,
      total: 3,
      complete: false
    }
  })
  const view = mount(HomeView, { attachTo: document.body, global: { plugins: [createPinia()] } })
  try {
    await flushPromises()
    expect(mocks.funds).not.toHaveBeenCalled()
    expect(view.get('.hero-change').text()).not.toContain('9,634.70')
    expect(view.get('.hero-change-context time').attributes('datetime')).toBe(disclosureDate)
    await view.get('.hero-change').trigger('click')
    await flushPromises()
    let sheet = document.body.querySelector('[role="dialog"]')!
    expect(sheet.textContent).toContain('最近已披露基金收益')
    expect(sheet.textContent).not.toContain('9,198.06')
    sheet.querySelector<HTMLButtonElement>('.sheet-close')!.click()
    await flushPromises()

    await view.get('button[aria-label="显示资产金额"]').trigger('click')
    expect(view.get('.hero-change b').text()).toBe('−9,634.70')
    expect(view.get('.hero-change .change-percent').text()).toBe('-1.55%')
    await view.get('.hero-change').trigger('click')
    await flushPromises()
    sheet = document.body.querySelector('[role="dialog"]')!
    expect(sheet.querySelector('.fund-disclosure')?.textContent).toContain('−9,198.06')
    expect(sheet.querySelector('.fund-disclosure time')?.getAttribute('datetime')).toBe(disclosureDate)
    expect(sheet.querySelector('.fund-disclosure')?.textContent).toContain('2/3')
    expect(sheet.querySelector('.breakdown-total b')?.textContent).toBe('−9,634.70')
  } finally {
    view.unmount()
  }
})

test('does not add the fund profit twice when the NAV date matches the daily change', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start, {
    fund: { isConfigured: true, amount: 1000, note: null, notHeld: false }
  }))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...monthChange,
    to: today,
    hasComparableData: true,
    comparableChange: -20,
    assetChanges: [
      { assetType: 'fund', change: -20, baselineAmount: 1020, isNew: false }
    ],
    priorFundDisclosure: null
  })

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  try {
    await flushPromises()
    await view.get('button[aria-label="显示资产金额"]').trigger('click')
    expect(view.get('.hero-change b').text()).toBe('−20.00')
    expect(view.find('.hero-change-context').exists()).toBe(false)
    expect(view.get('.card-fund').findAll('.card-note > span').map(note => note.text())).toEqual(['−20.00', '−1.96%'])
  } finally {
    view.unmount()
  }
})

test('carries over the latest disclosed fund profit and rate to the fund card before today NAV updates', async () => {
  const start = shift(today, -30)
  const fridayDate = shift(today, -3)
  mocks.ledger.mockResolvedValue(overview(start, {
    fund: { isConfigured: true, amount: 273349.99, note: null, notHeld: false }
  }))
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...monthChange,
    to: today,
    hasComparableData: true,
    comparableChange: 0,
    comparableBase: 620000,
    assetChanges: [
      { assetType: 'cash', change: 0, baselineAmount: 244685, isNew: false },
      { assetType: 'fund', change: 0, baselineAmount: 273349.99, isNew: false }
    ],
    priorFundDisclosure: {
      date: fridayDate,
      profit: -9198.06,
      covered: 2,
      total: 2,
      complete: true
    }
  })
  mocks.change.mockResolvedValue({
    ...monthChange,
    hasComparableData: true,
    assetChanges: [
      { assetType: 'fund', change: 12000, baselineAmount: 261349.99, isNew: false }
    ]
  })

  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  try {
    await flushPromises()
    expect(mocks.funds).not.toHaveBeenCalled()
    const fundNotes = () => view.get('.card-fund').findAll('.card-note > span').map(note => note.text())

    // Masked by default
    expect(fundNotes()).toEqual(['••••', '••••'])

    await view.get('button[aria-label="显示资产金额"]').trigger('click')
    expect(fundNotes()).toEqual(['−9,198.06', '−3.26%'])

    // Switching to monthly scope uses monthly asset change without adding prior daily disclosure
    await view.findAll('.change-scope button').find(button => button.text() === '本月')!.trigger('click')
    expect(fundNotes()).toEqual(['+12,000.00', '+4.59%'])
  } finally {
    view.unmount()
  }
})


test('shows asset changes by selected period and masks all card figures with the overview eye', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start, {
    month: {
      income: 9800, expense: 3268.50, surplus: 6531.50, hasRecords: true,
      providentDeposit: 5440.68, providentWithdrawal: 1200.20
    },
    fund: { isConfigured: true, amount: 278788.02, note: '持有 1 只基金 · 待确认 1,800.00', notHeld: false },
    stock: { isConfigured: true, amount: 1500, note: '持有 1 只股票', notHeld: false }
  }))
  mocks.funds.mockResolvedValue({ funds: [] })
  mocks.trend.mockResolvedValue(trend(start))
  mocks.dayChange.mockResolvedValue({
    ...monthChange, hasComparableData: true, comparableChange: 319.54, comparableBase: 350000,
    assetChanges: [
      { assetType: 'cash', change: 319.54, baselineAmount: 10000, isNew: false },
      { assetType: 'fund', change: -400, baselineAmount: 10000, isNew: false },
      { assetType: 'stock', change: 1500, baselineAmount: 0, isNew: false },
      { assetType: 'gold', change: 0, baselineAmount: 10000, isNew: false }
    ]
  })
  mocks.change.mockResolvedValue({
    ...monthChange, hasComparableData: true,
    assetChanges: [{ assetType: 'cash', change: -1000, baselineAmount: 20000, isNew: false }]
  })
  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  const cardNotes = (key: string) =>
    view.get(`.card-${key}`).findAll('.card-note > span').map(note => note.text())
  const hiddenValues = /9,800\.00|3,268\.50|6,531\.50|1,800\.00|319\.54|3\.20%|5,441|1,200/
  expect(view.text()).not.toMatch(hiddenValues)
  expect(cardNotes('cash')).toEqual(['••••', '••••'])
  expect(cardNotes('providentFund')).toEqual(['缴存 ••••', '提取 ••••'])
  expect(view.find('.month-flow-segment').exists()).toBe(false)
  await view.get('.hero-amount button[aria-label="显示资产金额"]').trigger('click')
  expect(view.get('.month-flow').text()).toContain('¥9,800.00')
  expect(view.get('.month-flow').text()).toContain('¥3,268.50')
  expect(view.get('.month-surplus').text()).toContain('+6,531.50')
  expect(cardNotes('cash')).toEqual(['+319.54', '+3.20%'])
  expect(cardNotes('fund')).toEqual(['−400.00', '−4.00%'])
  expect(cardNotes('stock')).toEqual(['+1,500.00', '—'])
  expect(cardNotes('gold')).toEqual(['0.00', '0.00%'])
  expect(cardNotes('providentFund')).toEqual(['缴存 5,441', '提取 1,200'])
  expect(view.get('.card-fund').text()).not.toContain('1,800.00')

  await view.findAll('.change-scope button').find(button => button.text() === '本月')!.trigger('click')
  expect(cardNotes('cash')).toEqual(['−1,000.00', '−5.00%'])
  expect(cardNotes('fund')).toEqual(['—', '—'])
  expect(cardNotes('providentFund')).toEqual(['缴存 5,441', '提取 1,200'])
  view.unmount()
})

test('keeps the rendered trend period selected until the latest request finishes', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  mocks.trend.mockResolvedValue({ ...trend(start), range: 'week' })
  const view = mount(HomeView, { global: { plugins: [createPinia()] } })
  await flushPromises()

  let finishMonth!: (value: AssetTrend) => void
  let finishQuarter!: (value: AssetTrend) => void
  mocks.trend.mockReturnValueOnce(new Promise(resolve => { finishMonth = resolve }))
    .mockReturnValueOnce(new Promise(resolve => { finishQuarter = resolve }))

  const [, month, quarter] = view.findAll('.range-button')
  await month!.trigger('click')
  expect(view.get('.range-button.active').text()).toBe('7日')
  expect(view.get('.trend-panel').attributes('aria-busy')).toBe('true')
  expect(quarter!.attributes('disabled')).toBeUndefined()
  await quarter!.trigger('click')

  finishQuarter({ ...trend(start), range: 'quarter', points: [point(today, { totalAssets: 420000 })] })
  await flushPromises()
  await view.get('button[aria-label="显示资产金额"]').trigger('click')
  expect(view.get('.range-button.active').text()).toBe('3月')
  expect(view.get('.chart-readout').text()).toContain('¥420,000.00')

  finishMonth({ ...trend(start), range: 'month', points: [point(today, { totalAssets: 390000 })] })
  await flushPromises()
  expect(view.get('.range-button.active').text()).toBe('3月')
  expect(view.get('.chart-readout').text()).toContain('¥420,000.00')
  view.unmount()
})

test('mounts the baseline chart while trend is loading and reuses cached trend on return', async () => {
  const start = shift(today, -30)
  mocks.ledger.mockResolvedValue(overview(start))
  let finishInitialTrend!: (value: AssetTrend) => void
  mocks.trend.mockReturnValueOnce(new Promise(resolve => { finishInitialTrend = resolve }))

  const pinia = createPinia()
  const first = mount(HomeView, { global: { plugins: [pinia] } })
  await flushPromises()

  expect(first.find('.chart-stub').exists()).toBe(true)
  expect(first.find('.loading-state.is-chart').exists()).toBe(false)
  expect(first.get('.chart-readout').text()).toContain('正在读取7日趋势')

  finishInitialTrend({ ...trend(start), range: 'week', points: [point(today, { totalAssets: 360000 })] })
  await flushPromises()
  await first.get('button[aria-label="显示资产金额"]').trigger('click')
  expect(first.get('.chart-readout').text()).toContain('¥360,000.00')
  first.unmount()

  let finishRefreshTrend!: (value: AssetTrend) => void
  mocks.trend.mockReturnValueOnce(new Promise(resolve => { finishRefreshTrend = resolve }))

  const returned = mount(HomeView, { global: { plugins: [pinia] } })
  await flushPromises()
  expect(returned.find('.chart-stub').exists()).toBe(true)
  expect(returned.get('.chart-readout').text()).toContain('¥360,000.00')
  expect(returned.get('.chart-readout').text()).not.toContain('正在读取')

  finishRefreshTrend({ ...trend(start), range: 'week', points: [point(today, { totalAssets: 365000 })] })
  await flushPromises()
  expect(returned.get('.chart-readout').text()).toContain('¥365,000.00')
  returned.unmount()
})
