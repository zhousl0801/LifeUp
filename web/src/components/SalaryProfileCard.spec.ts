import { mount } from '@vue/test-utils'
import { expect, test } from 'vitest'
import type { SalaryProfile } from '../types/salary'
import SalaryProfileCard from './SalaryProfileCard.vue'

const profile: SalaryProfile = {
  baseMonthlySalary: 20_000,
  netMonthlySalary: 12_000,
  providentMonthly: 4_800,
  bonusMonths: 4,
  bonusNetAmount: 60_000,
  bonusPayoutMonth: 1,
  payDay: 15
}

test('shows the create action without a profile', async () => {
  const view = mount(SalaryProfileCard, { props: { profile: null } })

  expect(view.text()).toContain('建立工资档案')
  expect(view.text()).toContain('个人信息')

  const button = view.findAll('button').find(item => item.text() === '建立工资档案')!
  await button.trigger('click')

  expect(view.emitted('create')).toHaveLength(1)
  view.unmount()
})

test('masks the salary amounts by default and can reveal them', async () => {
  const view = mount(SalaryProfileCard, { props: { profile } })

  expect(view.text()).toContain('••••')
  expect(view.text()).not.toContain('12,000.00')
  expect(view.text()).not.toContain('60,000.00')

  const show = view.findAll('button').find(item => item.text() === '显示金额')!
  await show.trigger('click')

  expect(view.text()).toContain('12,000.00')
  expect(view.text()).toContain('4,800.00')
  expect(view.text()).toContain('60,000.00')
  expect(view.text()).toContain('204,000.00')
  expect(view.text()).toContain('57,600.00')
  expect(view.text()).not.toContain('4 个月')
  expect(view.text()).not.toContain('每年 12 月到账')
  expect(view.text()).not.toContain('80,000.00')
  expect(view.text()).not.toContain('20,000.00')

  const hide = view.findAll('button').find(item => item.text() === '隐藏金额')!
  await hide.trigger('click')

  expect(view.text()).toContain('••••')
  expect(view.text()).not.toContain('12,000.00')
})

test('emits edit from the card action', async () => {
  const view = mount(SalaryProfileCard, { props: { profile } })

  const edit = view.findAll('button').find(item => item.text() === '编辑')!
  expect(edit.classes()).toContain('is-brand')
  await edit.trigger('click')

  expect(view.emitted('edit')).toHaveLength(1)
  view.unmount()
})

test('hides the bonus block when there is no bonus', async () => {
  const view = mount(SalaryProfileCard, {
    props: { profile: { ...profile, bonusMonths: 0, bonusNetAmount: 0 } }
  })

  expect(view.find('.salary-bonus').exists()).toBe(false)
  const show = view.findAll('button').find(item => item.text() === '显示金额')!
  await show.trigger('click')
  expect(view.text()).toContain('144,000.00')
  view.unmount()
})
