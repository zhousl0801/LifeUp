import { flushPromises, mount } from '@vue/test-utils'
import { expect, test, vi } from 'vitest'
import LedgerMonthlyBudget from './LedgerMonthlyBudget.vue'

const mocks = vi.hoisted(() => ({ fetch: vi.fn(), update: vi.fn() }))
vi.mock('../api/ledger', () => ({ fetchMonthlyBudget: mocks.fetch, updateMonthlyBudget: mocks.update }))
vi.mock('../composables/useMessage', () => ({ toast: { success: vi.fn() } }))

test('shows the monthly budget only after setting it and distinguishes overspending', async () => {
  mocks.fetch.mockResolvedValue({ month: '2026-11', amount: null })
  mocks.update.mockImplementation(async (month, amount) => ({ month, amount }))
  const view = mount(LedgerMonthlyBudget, {
    props: { month: '2026-11', today: '2026-11-09', expense: 650 },
    global: { stubs: { teleport: true } }
  })

  try {
    await flushPromises()
    expect(view.get('.budget-action').text()).toBe('设置月度预算')
    expect(view.find('.budget-ring').exists()).toBe(false)

    await view.get('.budget-action').trigger('click')
    await view.get('input').setValue('1000')
    await view.get('form').trigger('submit')
    await flushPromises()
    expect(mocks.update).toHaveBeenCalledWith('2026-11', 1000)
    expect(view.get('button.budget-ring').text()).toContain('¥1,000.00')
    expect(view.get('button.budget-ring').text()).toContain('65%')
    expect(view.get('.budget-remaining').text()).toBe('剩余预算 ¥350.00')
    expect(view.get('.budget-daily').text()).toBe('每日可花 ¥15.91')

    await view.setProps({ expense: 1125.25 })
    expect(view.get('.budget-ring').text()).toContain('113%')
    expect(view.get('.budget-remaining').text()).toBe('超出预算 ¥125.25')
    expect(view.get('.budget-daily').text()).toBe('每日可花 ¥0.00')

    await view.get('button.budget-ring').trigger('click')
    expect((view.get('input').element as HTMLInputElement).value).toBe('1000.00')
    await view.get('form .secondary-button').trigger('click')
    await flushPromises()
    expect(mocks.update).toHaveBeenLastCalledWith('2026-11', null)
    expect(view.find('.budget-ring').exists()).toBe(false)
  } finally {
    view.unmount()
  }
})

test('includes the last day of the current month and omits a daily amount for a past month', async () => {
  mocks.fetch.mockResolvedValue({ month: '2028-02', amount: 1000 })
  const view = mount(LedgerMonthlyBudget, {
    props: { month: '2028-02', today: '2028-02-29', expense: 800 }
  })
  try {
    await flushPromises()
    expect(view.get('.budget-daily').text()).toBe('每日可花 ¥200.00')
    await view.setProps({ today: '2028-03-01' })
    expect(view.get('.budget-daily').text()).toBe('每日可花 —')
  } finally {
    view.unmount()
  }
})
