import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, expect, test, vi } from 'vitest'
import ProvidentView from './ProvidentView.vue'
import LedgerView from './LedgerView.vue'
import CashView from './CashView.vue'
import { ApiError } from '../api/client'
import { useMessages } from '../composables/useMessage'
import { returnToLedger } from '../utils/ledgerReturn'
import type { LedgerEntry } from '../types/entry'

const mocks = vi.hoisted(() => ({ provident: vi.fn(), deposit: vi.fn(), withdraw: vi.fn(), opening: vi.fn(), plans: vi.fn(), savePlan: vi.fn(), pausePlan: vi.fn(), resumePlan: vi.fn(), entries: vi.fn(), stats: vi.fn(), push: vi.fn(), cash: vi.fn(), reconcile: vi.fn() }))
vi.mock('vue-router', () => ({
  RouterLink: { props: ['to'], template: '<a><slot /></a>' },
  onBeforeRouteLeave: vi.fn(),
  useRoute: () => ({ name: 'ledger', query: {} }),
  useRouter: () => ({ push: mocks.push, replace: vi.fn() })
}))
vi.mock('../api/provident', () => ({ fetchProvidentOverview: mocks.provident, depositProvident: mocks.deposit, withdrawProvident: mocks.withdraw, setProvidentOpening: mocks.opening }))
vi.mock('../api/plans', () => ({ fetchPlanOverview: mocks.plans, savePlan: mocks.savePlan, pausePlan: mocks.pausePlan, resumePlan: mocks.resumePlan }))
vi.mock('../api/cash', () => ({ fetchCashOverview: mocks.cash, reconcileCash: mocks.reconcile }))
vi.mock('../api/entries', () => ({ fetchEntryPeriod: mocks.entries, fetchEntryStats: mocks.stats }))

const { messages, clear } = useMessages()

beforeEach(() => {
  clear()
  vi.clearAllMocks()
  mocks.provident.mockResolvedValue({ isConfigured: true, balance: 100, asOfDate: '2026-09-15', periodFrom: '2026-10-01', periodTo: '2026-10-31', monthDeposit: 0, monthWithdrawal: 0, netChange: 0, activities: [] })
  mocks.plans.mockResolvedValue({ plans: [], pendingOccurrences: [] })
})

test('provident list labels the query month separately from the balance date', async () => {
  const view = mount(ProvidentView)
  await flushPromises()
  expect(view.find('.section-head .meta').text()).toBe('2026年10月')
  expect(view.find('.hero-meta').text()).toContain('2026年9月15日')
  view.unmount()
})

test('provident amounts use the same gain/loss colors as profit', async () => {
  mocks.provident.mockResolvedValue({
    isConfigured: true,
    balance: 250,
    asOfDate: '2026-10-31',
    periodFrom: '2026-10-01',
    periodTo: '2026-10-31',
    monthDeposit: 200,
    monthWithdrawal: 50,
    netChange: 150,
    activities: [
      { id: 'd1', type: 'deposit', amount: 200, balanceAfter: 300, note: null },
      { id: 'w1', type: 'withdrawal', amount: 50, balanceAfter: 250, note: null }
    ]
  })
  const view = mount(ProvidentView)
  await flushPromises()

  const metrics = view.findAll('.hero-metrics b')
  expect(metrics[0]!.text()).toBe('+200.00')
  expect(metrics[0]!.classes()).toContain('amount-gain')
  expect(metrics[1]!.text()).toBe('−50.00')
  expect(metrics[1]!.classes()).toContain('amount-loss')
  expect(metrics[2]!.text()).toBe('+150.00')
  expect(metrics[2]!.classes()).toContain('amount-gain')

  const activityValues = view.findAll('.activity-value')
  expect(activityValues[0]!.classes()).toContain('amount-gain')
  expect(activityValues[1]!.classes()).toContain('amount-loss')
  view.unmount()
})

test('cash net change is colored by direction', async () => {
  mocks.cash.mockResolvedValue({
    balance: 800,
    version: 1,
    asOfDate: '2026-10-31',
    from: '2026-10-01',
    to: '2026-10-31',
    openingBalance: 1000,
    inflow: 0,
    outflow: 200,
    adjustmentTotal: 0,
    netChange: -200,
    activities: []
  })
  const view = mount(CashView)
  await flushPromises()

  const netChange = view.findAll('.hero-metrics b')[2]!
  expect(netChange.text()).toBe('−200.00')
  expect(netChange.classes()).toContain('amount-loss')
  view.unmount()
})

test('one provident form records the monthly deposit and withdrawal together', async () => {
  mocks.deposit.mockResolvedValue({ balance: 100, activity: { amount: 200 } })
  mocks.withdraw.mockResolvedValue({ balance: 50, activity: { amount: 50 } })
  const view = mount(ProvidentView, { attachTo: document.body })
  await flushPromises()
  await view.find('.action-primary').trigger('click')
  const [depositInput, withdrawalInput] = document.querySelectorAll('input[inputmode="decimal"]')
  ;(depositInput as HTMLInputElement).value = '200'
  depositInput!.dispatchEvent(new Event('input', { bubbles: true }))
  ;(withdrawalInput as HTMLInputElement).value = '50'
  withdrawalInput!.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
  document.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await flushPromises()
  expect(mocks.deposit).toHaveBeenCalledTimes(1)
  expect(mocks.withdraw).toHaveBeenCalledTimes(1)
  expect(messages.value.at(-1)?.text).toBe('公积金设置已保存')
  view.unmount()
})

test('provident form does not ask for a business date', async () => {
  const view = mount(ProvidentView, { attachTo: document.body })
  await flushPromises()
  await view.find('.action-primary').trigger('click')
  expect(document.querySelector('.sheet input[type="date"]')).toBeNull()
  view.unmount()
})

test('provident form sets the monthly deposit inside the deposit and withdrawal sheet', async () => {
  mocks.savePlan.mockResolvedValue({})
  const view = mount(ProvidentView, { attachTo: document.body })
  await flushPromises()
  expect(view.text()).not.toContain('设置每月缴存')

  await view.find('.action-primary').trigger('click')
  const toggle = document.querySelector('.monthly-toggle input') as HTMLInputElement | null
  expect(toggle).not.toBeNull()
  toggle!.click()
  await flushPromises()

  const monthlyInput = document.querySelectorAll('input[inputmode="decimal"]')[2] as HTMLInputElement
  monthlyInput.value = '800'
  monthlyInput.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
  document.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await flushPromises()

  expect(mocks.savePlan).toHaveBeenCalledWith(expect.objectContaining({
    type: 'providentDeposit',
    amount: 800,
    dayOfMonth: 15,
    cycle: 'monthly'
  }))
  expect(messages.value.at(-1)?.text).toBe('每月缴存已设置')
  view.unmount()
})

test('paged ledger distinguishes a partial day subtotal and appends without replacing earlier entries', async () => {
  const entries = Array.from({ length: 100 }, (_, id) => ({ id: String(id), kind: 'income', category: '工资', amount: 1, businessDate: '2026-10-21', note: null }))
  const summary = { period: 'month', from: '2026-10-01', to: '2026-10-31', income: 101, expense: 0, surplus: 101, page: 1, pageSize: 100, totalCount: 101 }
  mocks.entries.mockResolvedValueOnce({ ...summary, entries })
    .mockResolvedValueOnce({ ...summary, page: 2, entries: [{ ...entries[0], id: '100' }] })
  const view = mount(LedgerView)
  await flushPromises()
  expect(view.find('.day-totals').text()).toContain('已显示小计')
  expect(view.findAll('.entry-row')).toHaveLength(100)
  const more = view.findAll('button').find((button) => button.text().startsWith('加载更多'))!
  await more.trigger('click')
  await flushPromises()
  expect(mocks.entries.mock.calls[1]![0].page).toBe(2)
  expect(view.findAll('.entry-row')).toHaveLength(101)
  expect(view.find('.day-totals').text()).not.toContain('已显示小计')
  expect(view.find('.day-totals').text()).toContain('101.00')
  view.unmount()
})


test('cash conflict keeps the draft and requires a new version before confirming again', async () => {
  const cash = { balance: 1000, version: 0, asOfDate: '2026-10-21', from: '2026-10-01', to: '2026-10-31', openingBalance: 1000, inflow: 0, outflow: 0, adjustmentTotal: 0, netChange: 0, activities: [] }
  mocks.cash.mockResolvedValueOnce(cash).mockResolvedValue({ ...cash, balance: 1100, version: 1 })
  mocks.reconcile.mockRejectedValueOnce(new ApiError(409, '现金余额已变化，请重新确认'))
    .mockResolvedValueOnce({ isBalanced: false, difference: -200, cashBalance: 900, adjustment: { difference: -200 } })
  const view = mount(CashView, { attachTo: document.body })
  await flushPromises()
  await view.find('.reconcile-link').trigger('click')
  await flushPromises()
  const amount = document.querySelector('input[inputmode="decimal"]') as HTMLInputElement
  amount.value = '900'
  amount.dispatchEvent(new Event('input', { bubbles: true }))
  const reason = document.querySelector('input[maxlength="100"]') as HTMLInputElement
  reason.value = '核对'
  reason.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
  const save = () => Array.from(document.querySelectorAll('button')).find((button) => button.textContent?.trim() === '保存核对')!
  save().click()
  await flushPromises()
  expect(amount.value).toBe('900')
  expect(document.body.textContent).toContain('现金余额已变化，请重新确认')
  expect(mocks.reconcile.mock.calls[0]![0].expectedVersion).toBe(0)
  save().click()
  await flushPromises()
  expect(mocks.reconcile.mock.calls[1]![0].expectedVersion).toBe(1)
  expect(mocks.reconcile.mock.calls[1]![0].requestId).not.toBe(mocks.reconcile.mock.calls[0]![0].requestId)
  view.unmount()
})

test('a slower previous period cannot overwrite the latest chart', async () => {
  mocks.entries.mockResolvedValue({ entries: [], income: 0, expense: 0, surplus: 0 })
  let resolveOld!: (value: unknown) => void
  const stats = { points: [], incomeCategories: [], expenseCategories: [], coveredDays: 1, income: 0, expense: 202, expenseDailyAverage: 202, note: 'latest' }
  mocks.stats.mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve }))
    .mockResolvedValue(stats)
  const view = mount(LedgerView)
  await flushPromises()
  await view.find('[aria-label="收支图表"]').trigger('click')
  await view.find('[aria-label="上一周期"]').trigger('click')
  await flushPromises()
  resolveOld({ ...stats, expense: 101, note: 'old' })
  await flushPromises()
  expect(view.find('.stats-note').text()).toBe('latest')
  expect(view.find('.stats-panel').text()).toContain('202.00')
  view.unmount()
})

test('stock cash activity opens stocks rather than gold', async () => {
  mocks.cash.mockResolvedValue({ balance: 100, activities: [{ id: 'stock', type: 'assetTransfer', assetType: 'stock', title: '股票买入', amount: -10, businessDate: '2026-11-01' }] })
  const view = mount(CashView)
  await flushPromises()
  await view.find('.activity-row').trigger('click')
  expect(mocks.push).toHaveBeenCalledWith({ name: 'stocks' })
  view.unmount()
})


test('saving outside the current ledger filters keeps the list and shows a concise toast without navigation', async () => {
  const previous = window.history.state
  const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
  const entry: LedgerEntry = {
    id: 'new-expense', kind: 'expense', category: '交通', amount: 8,
    businessDate: '2026-11-18', note: '地铁', source: 'manual', status: 'active',
    createdAt: '2026-11-18T00:00:00Z', updatedAt: '2026-11-18T00:00:00Z',
    reversedAt: null, reversalReason: null
  }
  window.history.replaceState({
    ...previous,
    ledgerContext: {
      period: 'month', anchor: '2026-10-01', view: 'list', selectedDate: '2026-10-01',
      kind: 'expense', category: '餐饮', search: '午餐', statsKind: 'expense', page: 1, scrollTop: 126
    }
  }, '')
  returnToLedger({ action: 'create', entry })
  mocks.entries.mockResolvedValue({ page: 1, totalCount: 0, entries: [] })
  mocks.stats.mockResolvedValue({ expense: 300, points: [] })
  const view = mount(LedgerView)
  try {
    await flushPromises()
    expect(view.get('.period-nav strong').text()).toContain('10月')
    expect(view.get('[aria-label="账单列表"]').attributes('aria-pressed')).toBe('true')
    expect(view.get<HTMLInputElement>('input[type="search"]').element.value).toBe('午餐')
    expect(scrollTo).toHaveBeenCalledWith({ top: 126 })
    expect(messages.value.at(-1)?.text).toBe('已保存')
    expect(mocks.push).not.toHaveBeenCalled()
  } finally {
    view.unmount()
    window.history.replaceState(previous, '')
    scrollTo.mockRestore()
  }
})

test('restores a scrolled chart after the chart data has rendered', async () => {
  const previous = window.history.state
  const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
  let resolveStats!: (value: unknown) => void
  mocks.entries.mockResolvedValue({ page: 1, totalCount: 0, entries: [] })
  mocks.stats.mockReturnValueOnce(new Promise(resolve => { resolveStats = resolve }))
  window.history.replaceState({
    ...previous,
    ledgerContext: {
      period: 'month', anchor: '2026-10-01', view: 'chart', selectedDate: '2026-10-01',
      kind: 'all', category: '', search: '', statsKind: 'expense', statsByGroup: true, page: 1, scrollTop: 600
    }
  }, '')
  const view = mount(LedgerView)
  try {
    await flushPromises()
    expect(scrollTo).not.toHaveBeenCalled()
    resolveStats({ points: [], incomeCategories: [], expenseCategories: [], coveredDays: 1, income: 0, expense: 202, expenseDailyAverage: 202 })
    await flushPromises()
    expect(view.get('.stats-panel').text()).toContain('¥202.00')
    expect(view.findAll('.stats-direction button.active').map(button => button.text())).toContain('一级分类')
    expect(scrollTo).toHaveBeenCalledWith({ top: 600 })
  } finally {
    view.unmount()
    window.history.replaceState(previous, '')
    scrollTo.mockRestore()
  }
})
