// @vitest-environment jsdom
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, test, vi } from 'vitest'
import LedgerStatsPanel from './LedgerStatsPanel.vue'
import type { EntryStats } from '../types/entry'

const mocks = vi.hoisted(() => ({
  fetchEntryPeriod: vi.fn(),
  fetchMonthlyBudget: vi.fn()
}))

vi.mock('../api/entries', () => ({
  fetchEntryPeriod: mocks.fetchEntryPeriod
}))

vi.mock('../api/ledger', () => ({
  fetchMonthlyBudget: mocks.fetchMonthlyBudget,
  updateMonthlyBudget: vi.fn()
}))

vi.mock('../utils/chartRuntime', () => ({
  loadChartRuntime: vi.fn(() => new Promise(() => {}))
}))

const sampleStats: EntryStats = {
  period: 'month',
  anchor: '2026-11-23',
  from: '2026-11-01',
  to: '2026-11-30',
  coveredDays: 23,
  income: 12000,
  expense: 4280.5,
  incomeDailyAverage: 521.74,
  expenseDailyAverage: 186.11,
  points: [
    { date: '2026-11-01', label: '1日', income: 12000, expense: 1200, category: '房租' },
    { date: '2026-11-05', label: '5日', income: 0, expense: 3080.5, category: '餐饮' }
  ],
  incomeCategories: [
    { category: '工资', amount: 12000, count: 1, share: 100, previousAmount: 10000, parentGroup: '经常收入' }
  ],
  expenseCategories: [
    { category: '餐饮', amount: 3080.5, count: 16, share: 71.97, previousAmount: 2800, parentGroup: '必要' },
    { category: '房租', amount: 1200, count: 1, share: 28.03, previousAmount: 1200, parentGroup: '固定' }
  ],
  incomeGroups: [
    { category: '经常收入', amount: 12000, count: 1, share: 100, previousAmount: 10000 }
  ],
  expenseGroups: [
    { category: '必要', amount: 3080.5, count: 16, share: 71.97, previousAmount: 2800 },
    { category: '固定', amount: 1200, count: 1, share: 28.03, previousAmount: 1200 }
  ],
  comparison: {
    comparable: true,
    label: '较上月同期',
    previousFrom: '2026-10-01',
    previousTo: '2026-10-23',
    previousIncome: 10000,
    previousExpense: 4673,
    previousSurplus: 5327,
    incomeDelta: 2000,
    incomeDeltaPercent: 20,
    expenseDelta: -392.5,
    expenseDeltaPercent: -8.4,
    surplusDelta: 2392.5
  },
  topExpenses: [
    { id: 'e-1', kind: 'expense', businessDate: '2026-11-01', category: '房租', note: '11月房租', amount: 1200 }
  ],
  topIncomes: [
    { id: 'i-1', kind: 'income', businessDate: '2026-11-01', category: '工资', note: '11月工资', amount: 12000 }
  ],
  note: null
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.fetchMonthlyBudget.mockResolvedValue({ amount: 6000 })
  mocks.fetchEntryPeriod.mockResolvedValue({
    page: 1,
    pageSize: 50,
    totalCount: 1,
    period: 'month',
    from: '2026-11-01',
    to: '2026-11-30',
    income: 0,
    expense: 3080.5,
    surplus: -3080.5,
    entries: [
      {
        id: 'e-2',
        kind: 'expense',
        category: '餐饮',
        amount: 320,
        businessDate: '2026-11-18',
        note: '聚餐',
        source: 'manual',
        status: 'active',
        createdAt: '2026-11-18T00:00:00Z',
        updatedAt: '2026-11-18T00:00:00Z',
        reversedAt: null,
        reversalReason: null
      }
    ]
  })
})

test('renders comparison badge, expands first-level group, and opens detail sheet for child category', async () => {
  const wrapper = mount(LedgerStatsPanel, {
    props: {
      stats: sampleStats,
      loading: false,
      period: 'month',
      anchor: '2026-11-23',
      today: '2026-11-23',
      periodLabel: '11月',
      mode: 'expense',
      byGroup: true,
      categories: [],
      categoryIconsByKind: new Map()
    }
  })

  await flushPromises()
  expect(wrapper.text()).toContain('较上月同期 -8.4%')
  expect(wrapper.text()).toContain('11月房租')

  await wrapper.find('.rank-row').trigger('click')
  expect(wrapper.find('.sub-row').text()).toContain('餐饮')

  await wrapper.find('.sub-row').trigger('click')
  await flushPromises()
  expect(mocks.fetchEntryPeriod).toHaveBeenCalledWith(expect.objectContaining({
    period: 'month',
    anchor: '2026-11-23',
    kind: 'expense',
    category: '餐饮',
    sort: 'amountDesc'
  }))
  expect(document.body.textContent).toContain('聚餐')
  wrapper.unmount()
})
