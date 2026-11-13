import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, expect, test } from 'vitest'
import { nextTick } from 'vue'
import type { SalaryProfile } from '../types/salary'
import SalaryProfileSheet from './SalaryProfileSheet.vue'

const profile: SalaryProfile = {
  baseMonthlySalary: 20_000,
  netMonthlySalary: 12_000,
  providentMonthly: 4_800,
  bonusMonths: 4,
  bonusNetAmount: 60_000,
  bonusPayoutMonth: 1,
  payDay: 15
}

let view: VueWrapper | null = null

function mountSheet(overrides: Partial<{
  open: boolean
  profile: SalaryProfile | null
  prefill: { netMonthlySalary: number | null; providentMonthly: number | null; payDay: number | null }
  saving: boolean
  serverError: string | null
}> = {}) {
  view = mount(SalaryProfileSheet, {
    attachTo: document.body,
    props: {
      open: true,
      profile: null,
      prefill: { netMonthlySalary: 12_000, providentMonthly: 4_800, payDay: 15 },
      saving: false,
      serverError: null,
      ...overrides
    }
  })

  return view
}

function input(label: string): HTMLInputElement | null {
  return document.querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)
}

afterEach(() => {
  view?.unmount()
  view = null
  document.body.innerHTML = ''
})

test('renders the primary save action', async () => {
  mountSheet()
  await flushPromises()

  const save = Array.from(document.querySelectorAll('button'))
    .find(button => button.textContent?.trim() === '保存工资档案')!

  expect(save.classList.contains('primary-button')).toBe(true)
  expect(save.classList.contains('salary-save')).toBe(true)
})

test('prefills the salary and provident plans for a new profile', async () => {
  mountSheet()
  await flushPromises()

  expect(input('每月税后工资')?.value).toBe('12000')
  expect(input('公积金月缴存')?.value).toBe('4800')
  expect(input('发薪日')?.value).toBe('15')
  expect(input('年终奖月数')?.value).toBe('0')
})

test('rejects a profile without base salary', async () => {
  mountSheet()
  await flushPromises()

  document.querySelector('.salary-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await nextTick()

  expect(view!.emitted('save')).toBeUndefined()
  expect(document.body.textContent).toContain('税前 Base 必须大于 0')
})

test('rejects bonus months without a net bonus', async () => {
  mountSheet()
  await flushPromises()

  input('税前 Base')!.value = '20000'
  input('税前 Base')!.dispatchEvent(new Event('input', { bubbles: true }))
  input('年终奖月数')!.value = '4'
  input('年终奖月数')!.dispatchEvent(new Event('input', { bubbles: true }))
  await nextTick()

  document.querySelector('.salary-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await nextTick()

  expect(view!.emitted('save')).toBeUndefined()
  expect(document.body.textContent).toContain('年终奖到手必须大于 0')
})

test('emits the parsed profile and annual summary', async () => {
  mountSheet()
  await flushPromises()

  for (const [label, value] of [
    ['税前 Base', '20000'],
    ['每月税后工资', '12000'],
    ['公积金月缴存', '4800'],
    ['年终奖月数', '4'],
    ['年终奖到手', '60000']
  ] as const) {
    const element = input(label)!
    element.value = value
    element.dispatchEvent(new Event('input', { bubbles: true }))
  }

  await nextTick()
  expect(document.body.textContent).toContain('320,000.00')
  expect(document.body.textContent).toContain('204,000.00')
  expect(document.body.textContent).toContain('57,600.00')

  document.querySelector('.salary-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
  await nextTick()

  expect(view!.emitted('save')?.[0]?.[0]).toEqual(profile)
})

test('confirms before disabling an existing profile', async () => {
  mountSheet({ profile })
  await flushPromises()

  const disable = Array.from(document.querySelectorAll('button'))
    .find(button => button.textContent?.trim() === '停用工资档案')!
  disable.click()
  await flushPromises()

  const confirm = Array.from(document.querySelectorAll('button'))
    .find(button => button.textContent?.trim() === '停用')!
  confirm.click()
  await flushPromises()

  expect(view!.emitted('disable')).toHaveLength(1)
})
