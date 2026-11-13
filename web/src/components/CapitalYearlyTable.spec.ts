import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import type { CapitalYearRow } from '../types/capital'
import CapitalYearlyTable from './CapitalYearlyTable.vue'

const years: CapitalYearRow[] = [
  {
    year: 2026,
    yearEndCapital: 384_050,
    netSavings: 9_000,
    providentDeposit: 4_000,
    return: 0,
    total: 13_000,
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
]

test('renders rows, expands details and shows the goal note', async () => {
  const view = mount(CapitalYearlyTable, {
    props: { years, etaMonth: '2033-06', targetAmount: 1_000_000, amountsMasked: false }
  })

  expect(view.findAll('.row-main')).toHaveLength(2)
  expect(view.find('.goal').text()).toContain('达成')
  expect(view.text()).toContain('预计 2033 年 6 月达成')

  await view.findAll('.row-main')[0]!.trigger('click')

  expect(view.find('.details').text()).toContain('净储蓄')
  expect(view.find('.details').text()).toContain('9,000.00')
  expect(view.find('.details').text()).toContain('4,000.00')
})

test('appends a goal row when the goal is beyond the visible years', () => {
  const view = mount(CapitalYearlyTable, {
    props: {
      years: years.filter((row) => !row.isGoalYear),
      etaMonth: '2033-06',
      targetAmount: 1_000_000,
      amountsMasked: false
    }
  })

  const rows = view.findAll('.row-main')
  expect(rows).toHaveLength(2)
  expect(rows[1]!.text()).toContain('2033')
  expect(rows[1]!.text()).toContain('达成')
})

test('masks amounts when requested', () => {
  const view = mount(CapitalYearlyTable, {
    props: { years, etaMonth: '2033-06', targetAmount: 1_000_000, amountsMasked: true }
  })

  expect(view.text()).not.toContain('384,050.00')
  expect(view.text()).toContain('¥3••••')
})
