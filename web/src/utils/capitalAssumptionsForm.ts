import type { CapitalEffectiveValue, CapitalGoalSettings } from '../types/capital'
import { formatMoney, formatSignedMoney, parseAmount, parseSignedAmount } from './format'

export type FieldKey =
  | 'targetAmount'
  | 'monthlyIncome'
  | 'monthlyExpense'
  | 'monthlyProvidentDeposit'
  | 'incomeGrowthPercent'
  | 'expenseGrowthPercent'
  | 'investmentAnnualReturnPercent'
  | 'providentAnnualReturnPercent'
  | 'annualBonus'
  | 'bonusPayoutMonth'

export const amountFields: FieldKey[] = ['monthlyIncome', 'monthlyExpense', 'monthlyProvidentDeposit', 'annualBonus']
export const monthFields: FieldKey[] = ['bonusPayoutMonth']
export const percentFields: FieldKey[] = [
  'incomeGrowthPercent',
  'expenseGrowthPercent',
  'investmentAnnualReturnPercent',
  'providentAnnualReturnPercent'
]
export const fieldKeys: FieldKey[] = ['targetAmount', ...amountFields, ...monthFields, ...percentFields]
export const investmentPresets = [
  { label: '纳指100 20年 ≈13%', value: 13 },
  { label: '标普500 20年 ≈10%', value: 10 }
]

export function parseInput(value: string, parser: (input: string) => number | null): number | null {
  const text = value.trim()

  return text ? parser(text) : null
}

export function parseTarget(value: string): number | null {
  const text = value.trim()

  if (!/^\d+(?:\.\d{1,4})?$/.test(text)) {
    return null
  }

  const wan = Number(text)

  if (!Number.isFinite(wan) || wan <= 0) {
    return null
  }

  const yuan = Math.round(wan * 10_000 * 100) / 100

  return yuan <= 1_000_000_000 ? yuan : null
}

export function formatWan(value: number | null): string {
  if (value === null) {
    return ''
  }

  return String(Number((value / 10_000).toFixed(4)))
}

export function toAssumptionsForm(settings: CapitalGoalSettings): Record<FieldKey, string> {
  const value = (number: number | null) => (number === null ? '' : String(number))

  return {
    targetAmount: formatWan(settings.targetAmount),
    monthlyIncome: value(settings.monthlyIncome),
    monthlyExpense: value(settings.monthlyExpense),
    monthlyProvidentDeposit: value(settings.monthlyProvidentDeposit),
    incomeGrowthPercent: value(settings.incomeGrowthPercent),
    expenseGrowthPercent: value(settings.expenseGrowthPercent),
    investmentAnnualReturnPercent: value(settings.investmentAnnualReturnPercent),
    providentAnnualReturnPercent: value(settings.providentAnnualReturnPercent),
    annualBonus: value(settings.annualBonus ?? null),
    bonusPayoutMonth: settings.bonusPayoutMonth == null ? '1' : String(settings.bonusPayoutMonth)
  }
}

export function toAssumptionsSettings(
  fields: Record<FieldKey, string>,
  useSalaryProfile: boolean
): CapitalGoalSettings {
  return {
    targetAmount: parseInput(fields.targetAmount, parseTarget),
    monthlyIncome: parseInput(fields.monthlyIncome, parseAmount),
    monthlyExpense: parseInput(fields.monthlyExpense, parseAmount),
    monthlyProvidentDeposit: parseInput(fields.monthlyProvidentDeposit, parseAmount),
    incomeGrowthPercent: parseInput(fields.incomeGrowthPercent, parseSignedAmount),
    expenseGrowthPercent: parseInput(fields.expenseGrowthPercent, parseSignedAmount),
    investmentAnnualReturnPercent: parseInput(fields.investmentAnnualReturnPercent, parseSignedAmount),
    providentAnnualReturnPercent: parseInput(fields.providentAnnualReturnPercent, parseSignedAmount),
    annualBonus: parseInput(fields.annualBonus, parseAmount),
    bonusPayoutMonth: Number(fields.bonusPayoutMonth),
    useSalaryProfile
  }
}

export function validateAssumptionsFields(
  fields: Record<FieldKey, string>,
  followProfile: boolean
): Partial<Record<FieldKey, string>> {
  const errors: Partial<Record<FieldKey, string>> = {}
  const targetText = fields.targetAmount.trim()

  if (targetText && parseTarget(targetText) === null) {
    errors.targetAmount = '目标金额必须大于零，最多四位小数（万元）。'
  }

  const followedFields = followProfile
    ? new Set<FieldKey>(['monthlyIncome', 'monthlyProvidentDeposit', 'annualBonus', 'bonusPayoutMonth'])
    : new Set<FieldKey>()

  for (const key of amountFields) {
    if (followedFields.has(key)) {
      continue
    }

    const text = fields[key].trim()

    if (text && parseAmount(text) === null) {
      errors[key] = '金额必须不小于零，最多两位小数。'
    }
  }

  if (!followedFields.has('bonusPayoutMonth')) {
    const month = Number(fields.bonusPayoutMonth)

    if (!Number.isInteger(month) || month < 1 || month > 12) {
      errors.bonusPayoutMonth = '发放月需在 1 到 12 之间。'
    }
  }

  for (const key of percentFields) {
    const text = fields[key].trim()

    if (!text) {
      continue
    }

    const value = parseSignedAmount(text)

    if (value === null || value <= -100 || value > 100) {
      errors[key] = '百分比必须在 -100 到 100 之间，最多两位小数。'
    }
  }

  return errors
}

export function amountHint(value: CapitalEffectiveValue): string {
  if (value.source === 'user') {
    return '已设定'
  }

  if (value.source === 'salary') {
    return '来自工资档案'
  }

  if (value.source === 'history') {
    return `最近均值 ${formatMoney(value.value)}`
  }

  return '暂无历史，按 0'
}

export function formatRate(value: number | null): string {
  return value === null ? '—' : `${value.toFixed(1)}%`
}

export function percentHint(value: CapitalEffectiveValue): string {
  if (value.source === 'user') {
    return '已设定'
  }

  return `默认 ${formatRate(value.value)}`
}

export function formatNet(value: number | null): string {
  return value === null ? '—' : formatSignedMoney(value)
}
