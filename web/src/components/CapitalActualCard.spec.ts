import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import type { CapitalActualMonth } from '../types/capital'
import CapitalActualCard from './CapitalActualCard.vue'

const actual: CapitalActualMonth = {
  month: '2026-11',
  comparableChange: 3_200,
  netDeposit: 6_800,
  marketValueChange: -3_600,
  expectedNetDeposit: 6_800,
  isComplete: true,
  note: null
}

test('renders actual, expected and the difference, then emits open-change', async () => {
  const view = mount(CapitalActualCard, { props: { actual, amountsMasked: false } })

  expect(view.text()).toContain('+3,200.00')
  expect(view.text()).toContain('+6,800.00')
  expect(view.text()).toContain('少 3,600.00')

  await view.find('.link').trigger('click')
  expect(view.emitted('open-change')).toHaveLength(1)
})

test('shows a dash and the note when the month is not comparable', () => {
  const view = mount(CapitalActualCard, {
    props: {
      actual: { ...actual, comparableChange: null, marketValueChange: null, isComplete: false, note: '本月行情依据不足' },
      amountsMasked: false
    }
  })

  expect(view.text()).toContain('—')
  expect(view.text()).toContain('本月行情依据不足')
})

test('masks amounts when requested', () => {
  const view = mount(CapitalActualCard, { props: { actual, amountsMasked: true } })

  expect(view.text()).toContain('¥••••')
  expect(view.text()).not.toContain('3,200.00')
})

test('compares actual expenses to the FIRE baseline even when amounts are masked', async () => {
  const view = mount(CapitalActualCard, {
    props: { actual: { ...actual, actualExpense: 4800 }, amountsMasked: false, isFireMode: true, fireBaselineMonthlyExpense: 6000 }
  })
  expect(view.text()).toContain('占 FIRE 基准（¥6,000.00/月）的 80%')
  await view.setProps({ amountsMasked: true })
  expect(view.text()).toContain('本月支出 ••••')
  expect(view.text()).not.toContain('6,000.00')
  await view.setProps({ actual: { ...actual, actualExpense: 6200 } })
  expect(view.text()).toContain('高于 FIRE 基准')
})
