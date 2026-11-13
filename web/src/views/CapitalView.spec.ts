import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { expect, test, vi } from 'vitest'
import { nextTick } from 'vue'
import CapitalView from './CapitalView.vue'
import CapitalAssumptionsSheet from '../components/CapitalAssumptionsSheet.vue'
import { ApiError } from '../api/client'
import { useHomePreferencesStore } from '../stores/homePreferences'
import type {
  CapitalEffectiveInputs,
  CapitalGoalSettings,
  CapitalOverview,
  CapitalProjection
} from '../types/capital'

const mocks = vi.hoisted(() => ({
  capital: vi.fn(),
  preview: vi.fn(),
  save: vi.fn(),
  monthChange: vi.fn(),
  budget: vi.fn(),
  plans: vi.fn(),
  stats: vi.fn()
}))

vi.mock('../api/capital', () => ({
  fetchCapital: mocks.capital,
  previewCapital: mocks.preview,
  saveCapital: mocks.save
}))
vi.mock('../api/ledger', () => ({
  fetchMonthChange: mocks.monthChange,
  fetchMonthlyBudget: mocks.budget
}))
vi.mock('../api/plans', () => ({ fetchPlanOverview: mocks.plans }))
vi.mock('../api/entries', () => ({ fetchEntryStats: mocks.stats }))
vi.mock('../components/GlassTrack.vue', () => ({
  default: {
    name: 'GlassTrack',
    props: [
      'progressPercent',
      'currentLabel',
      'etaLabel',
      'years',
      'milestones',
      'status',
      'amountsMasked',
      'animateUpdates',
      'ariaSummary'
    ],
    template: '<div class="track-stub" />'
  }
}))
vi.mock('../components/CapitalProjectionChart.vue', () => ({
  default: {
    name: 'CapitalProjectionChart',
    template: '<div class="chart-stub" />'
  }
}))
vi.mock('../components/CapitalAssumptionsSheet.vue', () => ({
  default: {
    name: 'CapitalAssumptionsSheet',
    props: ['open', 'settings', 'effective', 'suggestions', 'preview', 'saving', 'serverError'],
    emits: ['close', 'preview', 'save'],
    template: '<div v-if="open" class="sheet-stub"><span class="server-error">{{ serverError }}</span></div>'
  }
}))

const settings: CapitalGoalSettings = {
  targetAmount: 1_000_000,
  monthlyIncome: 12_000,
  monthlyExpense: 7_200,
  monthlyProvidentDeposit: 2_000,
  incomeGrowthPercent: 3,
  expenseGrowthPercent: 2,
  investmentAnnualReturnPercent: 5,
  providentAnnualReturnPercent: 1.5
}

const effective: CapitalEffectiveInputs = {
  monthlyIncome: { value: 12_000, source: 'user' },
  monthlyExpense: { value: 7_200, source: 'user' },
  monthlyProvidentDeposit: { value: 2_000, source: 'user' },
  incomeGrowthPercent: { value: 3, source: 'user' },
  expenseGrowthPercent: { value: 2, source: 'user' },
  investmentAnnualReturnPercent: { value: 5, source: 'user' },
  providentAnnualReturnPercent: { value: 1.5, source: 'user' }
}

const baseProjection: CapitalProjection = {
  status: 'onTrack',
  targetAmount: 1_000_000,
  remaining: 624_000,
  progressPercent: 37.6,
  etaMonth: '2033-06',
  monthsRemaining: 79,
  monthlyNetAccumulation: 6_800,
  blendedAnnualReturnPercent: 4.2,
  points: [{ month: '2026-12', capital: 384_050, netAccumulation: 6_800, return: 1_250 }],
  years: [
    {
      year: 2026,
      yearEndCapital: 384_050,
      netSavings: 4_800,
      providentDeposit: 2_000,
      return: 1_250,
      total: 8_050,
      isGoalYear: false
    },
    {
      year: 2033,
      yearEndCapital: 1_000_000,
      netSavings: 100_000,
      providentDeposit: 20_000,
      return: 50_000,
      total: 170_000,
      isGoalYear: true
    }
  ],
  milestones: [
    { thresholdAmount: 100_000, progressPercent: 10, date: '2026-10-21', title: '资本突破 10 万' }
  ],
  notes: []
}

function overview(overrides: Partial<CapitalOverview> = {}): CapitalOverview {
  return {
    settings,
    effective,
    current: {
      asOfDate: '2026-11-12',
      total: 376_000,
      cash: 26_000,
      investment: 300_000,
      provident: 50_000,
      coverageComplete: true,
      note: null
    },
    projection: baseProjection,
    actualMonth: {
      month: '2026-11',
      comparableChange: 3_200,
      netDeposit: 6_800,
      marketValueChange: -3_600,
      expectedNetDeposit: 6_800,
      isComplete: true,
      note: null
    },
    ...overrides
  }
}

const Empty = { template: '<div />' }

async function createTestRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/capital', name: 'capital', component: Empty },
      { path: '/plans', name: 'plans', component: Empty },
      { path: '/setup', name: 'setup', component: Empty },
      { path: '/assets', name: 'assets', component: Empty },
      { path: '/ledger', name: 'ledger', component: Empty },
      { path: '/', name: 'home', component: Empty },
      { path: '/mine', name: 'mine', component: Empty }
    ]
  })

  await router.push('/capital')
  await router.isReady()

  return router
}

async function mountView(data: CapitalOverview) {
  mocks.capital.mockResolvedValue(data)
  mocks.plans.mockResolvedValue({
    plans: [],
    pendingOccurrences: [],
    activeCount: 0,
    nextDueDate: null,
    cashBalance: 0,
    providentBalance: 0
  })
  mocks.budget.mockResolvedValue({ month: '2026-11', amount: null })
  mocks.stats.mockResolvedValue({ expense: 0 })
  const router = await createTestRouter()
  const pinia = createPinia()
  useHomePreferencesStore(pinia).amountsMasked = false
  const view = mount(CapitalView, { global: { plugins: [router, pinia] } })

  await flushPromises()

  return view
}

async function openSheet(view: ReturnType<typeof mountView> extends Promise<infer T> ? T : never) {
  await view.find('.header-action').trigger('click')
  await nextTick()

  return view.findComponent(CapitalAssumptionsSheet)
}

test('renders the on-track hero, chart and table', async () => {
  const view = await mountView(overview())

  expect(view.text()).toContain('376,000.00')
  expect(view.text()).toContain('100 万')
  expect(view.text()).toContain('62.4 万')
  expect(view.text()).toContain('+6,800.00')
  expect(view.text()).toContain('4.2%')
  expect(view.text()).toContain('预计 2033 年 6 月达成')
  expect(view.find('.track-stub').exists()).toBe(true)
  expect(view.find('.chart-stub').exists()).toBe(true)
})

test('shows the no-target CTA and opens the assumptions sheet', async () => {
  const view = await mountView(overview({
    projection: {
      ...baseProjection,
      status: 'noTarget',
      targetAmount: null,
      remaining: null,
      progressPercent: 0,
      etaMonth: null,
      monthsRemaining: null,
      milestones: []
    }
  }))

  expect(view.text()).toContain('还没有目标')
  expect(view.text()).toContain('先定一个数字')
  expect(view.find('.set-goal-pulse').exists()).toBe(true)

  await view.find('.set-goal').trigger('click')
  await nextTick()

  expect(view.find('.sheet-stub').exists()).toBe(true)
})

test('renders the reached state without the projection cards', async () => {
  const view = await mountView(overview({
    projection: {
      ...baseProjection,
      status: 'reached',
      targetAmount: 300_000,
      remaining: 0,
      progressPercent: 100,
      etaMonth: null,
      monthsRemaining: 0
    }
  }))

  expect(view.text()).toContain('目标已达成')
  expect(view.find('.chart-stub').exists()).toBe(false)
})

test('shows the depleted status and the incomplete coverage note', async () => {
  const view = await mountView(overview({
    current: {
      ...overview().current,
      coverageComplete: false,
      note: '部分资产未录入或估值不足，按已记录资产推演。'
    },
    projection: {
      ...baseProjection,
      status: 'depleted',
      etaMonth: null,
      monthsRemaining: null,
      notes: ['按当前资产结构推演', '按当前假设，资本将在 2028-07 耗尽。']
    }
  }))

  expect(view.text()).toContain('已记录资产')
  expect(view.text()).toContain('部分资产未录入')
  expect(view.text()).toContain('资本将耗尽')
  expect(view.text()).toContain('2028-07')
})

test('passes plan, budget and provident suggestions to the sheet', async () => {
  const view = await mountView(overview())
  mocks.plans.mockResolvedValue({
    plans: [
      { type: 'salaryIncome', amount: 12_000, isActive: true },
      { type: 'salaryIncome', amount: 999, isActive: false },
      { type: 'providentDeposit', amount: 2_000, isActive: true }
    ],
    pendingOccurrences: [],
    activeCount: 2,
    nextDueDate: null,
    cashBalance: 0,
    providentBalance: 0
  })
  mocks.budget.mockResolvedValue({ month: '2026-11', amount: 7_200 })

  const sheet = await openSheet(view)
  await flushPromises()

  expect(sheet.props('suggestions')).toEqual({
    income: 12_000,
    expense: 7_200,
    providentDeposit: 2_000
  })
})

test('applies a preview to the page without saving', async () => {
  const view = await mountView(overview())
  const previewData = overview({
    current: { ...overview().current, total: 400_000 },
    projection: { ...baseProjection, progressPercent: 40 }
  })
  mocks.preview.mockResolvedValue(previewData)

  const sheet = await openSheet(view)
  sheet.vm.$emit('preview', settings)
  await flushPromises()

  expect(mocks.preview).toHaveBeenCalledWith(settings)
  expect(view.text()).toContain('400,000.00')
  expect(sheet.props('open')).toBe(true)
})

test('saves settings, closes the sheet and updates the page', async () => {
  const view = await mountView(overview())
  const saved = overview({ current: { ...overview().current, total: 380_000 } })
  mocks.save.mockResolvedValue(saved)

  const sheet = await openSheet(view)
  sheet.vm.$emit('save', settings)
  await flushPromises()

  expect(mocks.save).toHaveBeenCalledWith(settings)
  expect(view.text()).toContain('380,000.00')
  expect(sheet.props('open')).toBe(false)
})

test('ignores preview responses after the sheet closes', async () => {
  const view = await mountView(overview())
  const previewData = overview({ current: { ...overview().current, total: 400_000 } })
  let resolvePreview!: (value: CapitalOverview) => void
  mocks.preview.mockImplementationOnce(() => new Promise((resolve) => {
    resolvePreview = resolve
  }))

  const sheet = await openSheet(view)
  sheet.vm.$emit('preview', settings)
  await nextTick()
  sheet.vm.$emit('close')
  await nextTick()
  resolvePreview(previewData)
  await flushPromises()

  expect(view.text()).toContain('376,000.00')
  expect(view.text()).not.toContain('400,000.00')
})

test('offers setup when the ledger is not initialized', async () => {
  mocks.capital.mockRejectedValue(new ApiError(404, '账本尚未初始化'))
  const router = await createTestRouter()
  const pinia = createPinia()
  useHomePreferencesStore(pinia).amountsMasked = false
  const view = mount(CapitalView, { global: { plugins: [router, pinia] } })

  await flushPromises()

  expect(view.text()).toContain('去初始化账本')
  expect(view.find('.state-link').attributes('href')).toContain('setup')
})

test('keeps the sheet open and shows a 409 error', async () => {
  const view = await mountView(overview())
  mocks.save.mockRejectedValue(new ApiError(409, '账本有新变化'))

  const sheet = await openSheet(view)
  sheet.vm.$emit('save', settings)
  await flushPromises()

  expect(sheet.props('open')).toBe(true)
  expect(view.find('.server-error').text()).toContain('账本刚有新变化')
})
