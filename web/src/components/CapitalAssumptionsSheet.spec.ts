import { fireFixture } from '../test/fireFixture'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import type { CapitalEffectiveInputs, CapitalGoalSettings, CapitalOverview, CapitalSuggestions } from '../types/capital'
import CapitalAssumptionsSheet from './CapitalAssumptionsSheet.vue'

const settings: CapitalGoalSettings = {
  targetAmount: 1_000_000,
  monthlyIncome: 12_000,
  monthlyExpense: 7_200,
  monthlyProvidentDeposit: 2_000,
  incomeGrowthPercent: 3,
  expenseGrowthPercent: 2,
  investmentAnnualReturnPercent: 5,
  providentAnnualReturnPercent: 1.5,
  annualBonus: 60_000,
  bonusPayoutMonth: 1,
  useSalaryProfile: false
}

const effective: CapitalEffectiveInputs = {
  monthlyIncome: { value: 12_000, source: 'user' },
  monthlyExpense: { value: 7_200, source: 'user' },
  monthlyProvidentDeposit: { value: 2_000, source: 'user' },
  incomeGrowthPercent: { value: 3, source: 'user' },
  expenseGrowthPercent: { value: 2, source: 'user' },
  investmentAnnualReturnPercent: { value: 5, source: 'user' },
  providentAnnualReturnPercent: { value: 1.5, source: 'user' },
  annualBonus: { value: 60_000, source: 'user' },
  bonusPayoutMonth: { value: 1, source: 'user' }
}

const preview = {
  projection: {
    monthlyNetAccumulation: 8_200,
    blendedAnnualReturnPercent: 4.2
  }
} as CapitalOverview

let view: VueWrapper | null = null

const suggestions: CapitalSuggestions = {
  income: 12_000,
  expense: 7_200,
  providentDeposit: 2_000
}

function mountSheet(overrides: Partial<{
  open: boolean
  settings: CapitalGoalSettings
  effective: CapitalEffectiveInputs
  suggestions: CapitalSuggestions
  preview: CapitalOverview | null
  saving: boolean
  serverError: string | null
  hasSalaryProfile: boolean
  fire: typeof fireFixture
}> = {}) {
  view = mount(CapitalAssumptionsSheet, {
    props: {
      open: true,
      settings,
      effective,
      suggestions: { income: null, expense: null, providentDeposit: null },
      preview: null,
      saving: false,
      serverError: null,
      hasSalaryProfile: false,
      ...overrides
    }
  })

  return view
}

function input(label: string): HTMLInputElement {
  const element = document.querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)

  if (!element) {
    throw new Error(`input not found: ${label}`)
  }

  return element
}

async function setInput(label: string, value: string): Promise<void> {
  const element = input(label)
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
  await nextTick()
}

function findButton(text: string): HTMLButtonElement {
  const button = [...document.querySelectorAll<HTMLButtonElement>('button')]
    .find((item) => item.textContent?.includes(text))

  if (!button) {
    throw new Error(`button not found: ${text}`)
  }

  return button
}

async function bodySnapshot(): Promise<string> {
  await flushPromises()
  await nextTick()

  return document.body.textContent ?? ''
}

afterEach(() => {
  view?.unmount()
  view = null
  document.body.innerHTML = ''
  vi.useRealTimers()
})

describe('CapitalAssumptionsSheet', () => {
  it('prefills the saved settings and renders the effective hints', async () => {
    mountSheet({
      settings: { ...settings, monthlyIncome: null },
      effective: { ...effective, monthlyIncome: { value: 10_000, source: 'history' } }
    })
    const text = await bodySnapshot()

    expect(input('目标金额').value).toBe('100')
    expect(input('投资年化').value).toBe('5')
    expect(text).toContain('最近均值 10,000.00')
  })

  it('emits a debounced preview with parsed settings', async () => {
    vi.useFakeTimers()
    mountSheet()
    await flushPromises()

    await setInput('月收入', '15000')
    vi.advanceTimersByTime(300)
    await flushPromises()

    const emitted = view!.emitted('preview')?.at(-1)?.[0] as CapitalGoalSettings
    expect(emitted.monthlyIncome).toBe(15_000)
    expect(emitted.targetAmount).toBe(1_000_000)
  })

  it('shows a field error and does not save an invalid target', async () => {
    mountSheet()
    await bodySnapshot()

    await setInput('目标金额', '0')
    expect(await bodySnapshot()).toContain('目标金额必须大于零')

    findButton('保存并重新计算').click()
    await nextTick()

    expect(view!.emitted('save')).toBeUndefined()
  })

  it('shows a field error for an out-of-range percentage', async () => {
    mountSheet()
    await bodySnapshot()

    await setInput('投资年化', '101')
    expect(await bodySnapshot()).toContain('百分比必须在 -100 到 100 之间')
  })

  it('shows the preview derived values and the server error', async () => {
    mountSheet({ preview, serverError: '账本刚有新变化，请重试。' })
    const text = await bodySnapshot()

    expect(text).toContain('+8,200.00')
    expect(text).toContain('4.2%')
    expect(text).toContain('账本刚有新变化')
  })

  it('resets assumptions while keeping the target', async () => {
    mountSheet()
    await bodySnapshot()

    await setInput('月收入', '15000')
    findButton('恢复假设默认').click()
    await bodySnapshot()

    expect(input('目标金额').value).toBe('100')
    expect(input('月收入').value).toBe('')

    const emitted = view!.emitted('preview')?.at(-1)?.[0] as CapitalGoalSettings
    expect(emitted.targetAmount).toBe(1_000_000)
    expect(emitted.monthlyIncome).toBeNull()
  })

  it('groups related fields and defaults the target unit to 万元', async () => {
    mountSheet()
    await bodySnapshot()

    const labels = [...document.querySelectorAll('.field-control > span:not(.input-unit)')]
      .map((element) => element.textContent?.trim())

    expect(labels).toEqual([
      '目标金额（万元）',
      '月收入',
      '月支出',
      '公积金月缴存',
      '年终奖（到手）',
      '年终奖发放月',
      '公积金年化',
      '收入年增长',
      '支出年增长',
      '投资年化'
    ])
  })

  it('follows the salary profile and switches to manual with effective values', async () => {
    mountSheet({
      settings: { ...settings, useSalaryProfile: true },
      hasSalaryProfile: true
    })
    await bodySnapshot()

    expect(document.querySelector('input[aria-label="月收入"]')).toBeNull()
    expect(document.querySelector('input[aria-label="公积金月缴存"]')).toBeNull()
    expect(document.querySelector('input[aria-label="年终奖到手"]')).toBeNull()
    expect(document.body.textContent).toContain('跟随工资档案')

    findButton('改为手动').click()
    await nextTick()

    expect(input('月收入').value).toBe('12000')
    expect(input('公积金月缴存').value).toBe('2000')
    expect(input('年终奖到手').value).toBe('60000')

    findButton('保存并重新计算').click()
    await nextTick()

    expect(view!.emitted('save')?.[0]?.[0]).toMatchObject({
      monthlyIncome: 12_000,
      monthlyProvidentDeposit: 2_000,
      annualBonus: 60_000,
      useSalaryProfile: false
    })
  })

  it('keeps the retained manual payout month while following', async () => {
    mountSheet({
      settings: { ...settings, useSalaryProfile: true, bonusPayoutMonth: 12 },
      hasSalaryProfile: true
    })
    await bodySnapshot()

    findButton('保存并重新计算').click()
    await nextTick()

    expect(view!.emitted('save')?.[0]?.[0]).toMatchObject({
      useSalaryProfile: true,
      bonusPayoutMonth: 12
    })
  })

  it('switches back to following the salary profile', async () => {
    mountSheet({
      settings: { ...settings, useSalaryProfile: false, bonusPayoutMonth: 12 },
      hasSalaryProfile: true
    })
    await bodySnapshot()

    findButton('改用工资档案').click()
    await nextTick()

    expect(document.querySelector('input[aria-label="月收入"]')).toBeNull()
    expect(document.body.textContent).toContain('跟随工资档案')

    findButton('保存并重新计算').click()
    await nextTick()

    expect(view!.emitted('save')?.[0]?.[0]).toMatchObject({
      useSalaryProfile: true,
      bonusPayoutMonth: 12
    })
  })

  it('saves an empty bonus as null', async () => {
    mountSheet()
    await bodySnapshot()

    await setInput('年终奖到手', '')

    findButton('保存并重新计算').click()
    await nextTick()

    expect(view!.emitted('save')?.[0]?.[0]).toMatchObject({ annualBonus: null })
  })

  it('applies plan, budget and provident suggestions', async () => {
    mountSheet({ suggestions })
    await bodySnapshot()

    findButton('用计划收入').click()
    findButton('用账单预设').click()
    findButton('用缴存设置').click()
    await nextTick()

    expect(input('月收入').value).toBe('12000')
    expect(input('月支出').value).toBe('7200')
    expect(input('公积金月缴存').value).toBe('2000')
  })

  it('converts the target from 万元 to yuan', async () => {
    vi.useFakeTimers()
    mountSheet()
    await flushPromises()

    await setInput('目标金额', '120.5')
    vi.advanceTimersByTime(300)
    await flushPromises()

    const emitted = view!.emitted('preview')?.at(-1)?.[0] as CapitalGoalSettings
    expect(emitted.targetAmount).toBe(1_205_000)
  })

  it('applies the 20-year investment presets', async () => {
    mountSheet()
    await bodySnapshot()

    findButton('纳指100').click()
    await nextTick()
    expect(input('投资年化').value).toBe('13')

    findButton('标普500').click()
    await nextTick()
    expect(input('投资年化').value).toBe('10')
  })

  it('does not emit a preview after the sheet closes', async () => {
    vi.useFakeTimers()
    mountSheet()
    await flushPromises()

    await setInput('月收入', '15000')
    const before = view!.emitted('preview')?.length ?? 0

    await view!.setProps({ open: false })
    vi.advanceTimersByTime(300)
    await flushPromises()

    expect(view!.emitted('preview')?.length ?? 0).toBe(before)
  })

  it('asks before discarding unsaved changes', async () => {
    mountSheet()
    await bodySnapshot()

    await setInput('月收入', '15000')
    document.querySelector<HTMLButtonElement>('button[aria-label="关闭"]')!.click()
    expect(await bodySnapshot()).toContain('放弃修改？')

    findButton('继续编辑').click()
    await nextTick()
    expect(document.body.textContent ?? '').not.toContain('放弃修改？')

    document.querySelector<HTMLButtonElement>('button[aria-label="关闭"]')!.click()
    await nextTick()
    findButton('放弃修改').click()
    await nextTick()

    expect(view!.emitted('close')).toHaveLength(1)
  })

  it('prompts before discarding a salary profile follow switch and cancels pending preview on save', async () => {
    vi.useFakeTimers()
    mountSheet({
      settings: { ...settings, useSalaryProfile: false },
      hasSalaryProfile: true
    })
    await flushPromises()

    findButton('改用工资档案').click()
    await nextTick()

    document.querySelector<HTMLButtonElement>('button[aria-label="关闭"]')!.click()
    expect(await bodySnapshot()).toContain('放弃修改？')

    findButton('继续编辑').click()
    await nextTick()

    findButton('保存并重新计算').click()
    await nextTick()
    const previewsBeforeAdvance = view!.emitted('preview')?.length ?? 0

    vi.advanceTimersByTime(300)
    await flushPromises()
    expect(view!.emitted('preview')?.length ?? 0).toBe(previewsBeforeAdvance)
  })
})

it('cycles FIRE categories and preserves custom fields in the saved payload', async () => {
  const wrapper = mountSheet({ settings: { ...settings, goalMode: 'fire' }, fire: fireFixture })
  await bodySnapshot()
  expect(document.querySelector('[aria-label="标准退休月支出"]')).toBeNull()
  findButton('支出分类校准').click()
  await nextTick()
  findButton('房租').click()
  await nextTick()
  expect(findButton('房租').getAttribute('data-state')).toBe('standard')
  findButton('房租').click()
  await setInput('标准退休月支出', '4500.25')
  findButton('3.5%').click()
  await nextTick()
  findButton('保存并重新计算').click()
  expect(wrapper.emitted('save')?.[0]?.[0]).toMatchObject({
    goalMode: 'fire', targetAmount: 1000000, safeWithdrawalRatePercent: 3.5,
    excludedExpenseCategories: ['房租'], leanExpenseCategories: [], fireMonthlyExpenseOverride: 4500.25
  })
})

it('validates FIRE inputs and preserves both drafts when previewing a mode switch', async () => {
  vi.useFakeTimers()
  const wrapper = mountSheet({
    settings: { ...settings, goalMode: 'fire', fireMode: 'lean', safeWithdrawalRatePercent: 4 },
    fire: fireFixture
  })
  await bodySnapshot()

  await setInput('安全提取率', '3.555')
  await setInput('Coast 目标年份', '2201')
  findButton('支出分类校准').click()
  await nextTick()
  await setInput('Lean 退休月支出', '-1')
  vi.advanceTimersByTime(300)
  expect(wrapper.emitted('preview')).toBeUndefined()
  expect(await bodySnapshot()).toContain('安全提取率须为 1% 至 15%')
  expect(await bodySnapshot()).toContain('Coast 目标年份须为 2025 至 2200 的整数')
  expect(await bodySnapshot()).toContain('Lean 退休月支出：金额必须不小于零')

  await setInput('安全提取率', '1')
  await setInput('Coast 目标年份', '2200')
  await setInput('Lean 退休月支出', '0')
  findButton('自定义目标').click()
  await nextTick()
  expect(input('目标金额').value).toBe('100')
  vi.advanceTimersByTime(300)
  expect(wrapper.emitted('preview')?.at(-1)?.[0]).toMatchObject({
    goalMode: 'custom', fireMode: 'lean', safeWithdrawalRatePercent: 1,
    coastTargetYear: 2200, leanMonthlyExpenseOverride: 0, targetAmount: 1000000
  })
  findButton('FIRE 模式').click()
  await nextTick()
  expect(input('安全提取率').value).toBe('1')
  document.querySelector<HTMLButtonElement>('.sheet-close')!.click()
  expect(await bodySnapshot()).toContain('放弃修改？')
})
