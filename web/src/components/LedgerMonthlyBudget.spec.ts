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
    props: { month: '2026-11', expense: 650 },
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
    expect(view.get('.budget-ring').text()).toBe('65%')
    expect(view.get('.budget-remaining').text()).toBe('剩余预算 ¥350.00')

    await view.setProps({ expense: 1125.25 })
    expect(view.get('.budget-ring').text()).toBe('113%')
    expect(view.get('.budget-remaining').text()).toBe('超出预算 ¥125.25')

    await view.get('.budget-action').trigger('click')
    await view.get('form .secondary-button').trigger('click')
    await flushPromises()
    expect(mocks.update).toHaveBeenLastCalledWith('2026-11', null)
    expect(view.find('.budget-ring').exists()).toBe(false)
  } finally {
    view.unmount()
  }
})
