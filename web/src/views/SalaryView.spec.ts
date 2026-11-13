import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { beforeEach, expect, test, vi } from 'vitest'
import SalaryView from './SalaryView.vue'

const api = vi.hoisted(() => ({
  salary: vi.fn(),
  save: vi.fn(),
  disable: vi.fn(),
  plans: vi.fn()
}))

vi.mock('../api/salary', () => ({
  fetchSalaryProfile: api.salary,
  saveSalaryProfile: api.save,
  disableSalaryProfile: api.disable
}))
vi.mock('../api/plans', () => ({
  fetchPlanOverview: api.plans
}))

const Empty = { template: '<div />' }

async function createTestRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/salary', name: 'salary', component: Empty },
      { path: '/mine', name: 'mine', component: Empty },
      { path: '/plans', name: 'plans', component: Empty }
    ]
  })

  await router.push('/salary')
  await router.isReady()

  return router
}

const salaryProfile = {
  baseMonthlySalary: 20_000,
  netMonthlySalary: 12_000,
  providentMonthly: 4_800,
  bonusMonths: 4,
  bonusNetAmount: 60_000,
  bonusPayoutMonth: 1,
  payDay: 15
}

beforeEach(() => {
  vi.clearAllMocks()
  api.salary.mockResolvedValue({ profile: null })
  api.plans.mockResolvedValue({
    plans: [],
    pendingOccurrences: [],
    activeCount: 0,
    nextDueDate: null,
    cashBalance: 0,
    providentBalance: 0
  })
})

test('renders the salary card masked by default', async () => {
  api.salary.mockResolvedValue({ profile: salaryProfile })
  const router = await createTestRouter()
  const view = mount(SalaryView, { global: { plugins: [router] } })
  await flushPromises()

  expect(view.text()).toContain('工资档案')
  expect(view.find('.salary-card').text()).toContain('••••')
  expect(view.text()).not.toContain('12,000.00')

  await view.findAll('button').find(button => button.text() === '显示金额')!.trigger('click')
  expect(view.text()).toContain('12,000.00')
  view.unmount()
})

test('opens the edit sheet from the card', async () => {
  api.salary.mockResolvedValue({ profile: salaryProfile })
  const router = await createTestRouter()
  const view = mount(SalaryView, { global: { plugins: [router] } })
  await flushPromises()

  await view.findAll('button').find(button => button.text() === '编辑')!.trigger('click')
  await flushPromises()

  expect(document.querySelector<HTMLInputElement>('input[aria-label="税前 Base"]')!.value).toBe('20000')
  view.unmount()
})

test('creates a profile from existing plan prefill and saves it', async () => {
  api.plans.mockResolvedValue({
    plans: [
      {
        id: 'salary-plan',
        type: 'salaryIncome',
        name: '工资',
        amount: 12_000,
        cycle: 'monthly',
        dayOfMonth: 15,
        dayOfWeek: null,
        monthOfYear: null,
        time: '09:00:00',
        startDate: '2026-01-01',
        generateFrom: '2026-01-01',
        isActive: true,
        fundPositionId: null,
        fundPositionName: null,
        nextDueDate: null,
        version: 0
      },
      {
        id: 'provident-plan',
        type: 'providentDeposit',
        name: '公积金',
        amount: 4_800,
        cycle: 'monthly',
        dayOfMonth: 15,
        dayOfWeek: null,
        monthOfYear: null,
        time: '09:00:00',
        startDate: '2026-01-01',
        generateFrom: '2026-01-01',
        isActive: true,
        fundPositionId: null,
        fundPositionName: null,
        nextDueDate: null,
        version: 0
      }
    ],
    pendingOccurrences: [],
    activeCount: 2,
    nextDueDate: null,
    cashBalance: 0,
    providentBalance: 0
  })

  const router = await createTestRouter()
  const view = mount(SalaryView, { global: { plugins: [router] } })
  await flushPromises()

  await view.findAll('button').find(button => button.text() === '建立工资档案')!.trigger('click')
  await flushPromises()

  expect(document.querySelector<HTMLInputElement>('input[aria-label="每月税后工资"]')!.value).toBe('12000')
  expect(document.querySelector<HTMLInputElement>('input[aria-label="公积金月缴存"]')!.value).toBe('4800')

  const baseInput = document.querySelector<HTMLInputElement>('input[aria-label="税前 Base"]')!
  baseInput.value = '20000'
  baseInput.dispatchEvent(new Event('input', { bubbles: true }))
  api.salary.mockResolvedValue({ profile: { ...salaryProfile, bonusMonths: 0, bonusNetAmount: 0 } })
  api.save.mockResolvedValue({ profile: { ...salaryProfile, bonusMonths: 0, bonusNetAmount: 0 } })

  document.querySelector<HTMLButtonElement>('.salary-form button[type="submit"]')!.click()
  await flushPromises()

  expect(api.save).toHaveBeenCalledTimes(1)
  expect(api.save.mock.calls[0]![0]).toMatchObject({
    baseMonthlySalary: 20_000,
    netMonthlySalary: 12_000,
    providentMonthly: 4_800,
    bonusMonths: 0,
    payDay: 15
  })
  expect(view.find('.salary-card').text()).toContain('••••')
  view.unmount()
})
